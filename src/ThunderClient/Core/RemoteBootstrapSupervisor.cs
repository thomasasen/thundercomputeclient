using System.Text;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

/// <summary>
/// Detachable Thunder Ubuntu bootstrap using a private remote log and one
/// active run. Only reviewed adapter code, never secrets, is stored on disk.
/// </summary>
public static class RemoteBootstrapSupervisor
{
    public static string BuildLaunchScript(string reviewedAdapter, string commit,
        string workflows, string githubToken, string? huggingFace, string? civitai,
        bool restart = false)
    {
        var adapter = SshService.PinBootstrapAdapter(reviewedAdapter, commit);
        if (!Regex.IsMatch(workflows, @"\A[A-Za-z0-9_-]+(?:,[A-Za-z0-9_-]+)*\z") ||
            string.IsNullOrWhiteSpace(githubToken))
            throw new InvalidOperationException("Ungültiger Workflow oder fehlende Bootstrap-Credentials.");

        // The only file written on the pod is credential-free reviewed source.
        var worker = "#!/usr/bin/env bash\n" +
            "set -Eeuo pipefail\numask 077\n" +
            "exec 9>\"$HOME/.thunderclient-bootstrap.lock\"\n" +
            "if ! flock -n 9; then echo '[THUNDER-CLIENT] Ein Bootstrap ist bereits aktiv'; exit 42; fi\n" +
            adapter + "\n";
        var encodedWorker = Convert.ToBase64String(Encoding.UTF8.GetBytes(worker));
        var nextRun = Guid.NewGuid().ToString("N");
        var revision = commit.ToLowerInvariant();
        var restartFlag = restart ? "1" : "0";

        return $$"""
set -Eeuo pipefail
umask 077
for command in flock nohup setsid base64 tail grep ps sleep mktemp python3; do
    command -v "$command" >/dev/null 2>&1 || {
        printf '[THUNDER-CLIENT] Remote-Voraussetzung fehlt: %s\n' "$command"
        exit 41
    }
done
: "${HOME:?HOME fehlt}"
job="$HOME/.thunderclient-deployment"
if [[ -L "$job" ]]; then echo '[THUNDER-CLIENT] Unsicheres Job-Verzeichnis'; exit 41; fi
mkdir -p "$job"
chmod 700 "$job"
# All metadata is controlled by the authenticated ubuntu user. Never follow
# a pre-created symlink while writing credentials-free job metadata or logs.
for item in "$job/launch.lock" "$job/pid" "$job/run_id" "$job/commit"             "$job/workflows" "$job/bootstrap.sh" "$job/output.log"; do
    if [[ -L "$item" ]]; then echo '[THUNDER-CLIENT] Unsicherer Job-Pfad'; exit 41; fi
done
exec 8>"$job/launch.lock"
flock -w 15 8 || { echo '[THUNDER-CLIENT] Anderer Start läuft'; exit 42; }
active=''
if [[ -f "$job/pid" && -f "$job/run_id" ]]; then
    active_pid="$(cat "$job/pid")"
    active_run="$(cat "$job/run_id")"
    if [[ "$active_pid" =~ ^[0-9]+$ && "$active_run" =~ ^[a-f0-9]{32}$ ]] &&
       [[ -r "/proc/$active_pid/environ" ]] &&
       grep -azFq "THUNDER_CLIENT_RUN_ID=$active_run" "/proc/$active_pid/environ"; then
        active="$active_pid"
    fi
fi
# A running PID that no longer matches the recorded Run-ID is not
# ours to signal or replace. Do not overwrite job metadata in this case.
if [[ '{{restartFlag}}' == '1' && -z "$active" && -f "$job/pid" ]]; then
    previous_pid="$(cat "$job/pid")"
    if [[ "$previous_pid" =~ ^[0-9]+$ ]] && kill -0 "$previous_pid" 2>/dev/null; then
        echo '[THUNDER-CLIENT] Bisheriger PID läuft, gehört aber nicht eindeutig zum Remote-Job; Neustart gesperrt'
        exit 45
    fi
fi
# When our previous job already exited, another non-ThunderClient ComfyUI
# might be listening. Never launch a competing service over that port.
if [[ '{{restartFlag}}' == '1' && -z "$active" ]]; then
    if python3 - <<'PYPORT'
import socket
try:
    with socket.create_connection(('127.0.0.1', 8188), timeout=1):
        raise SystemExit(0)
except OSError:
    raise SystemExit(1)
PYPORT
    then
        echo '[THUNDER-CLIENT] Port 8188 ist durch einen fremden Prozess belegt; Neustart gesperrt'
        exit 45
    fi
fi
# Prepare the new, credential-free worker BEFORE stopping a healthy service.
# A failed download, validation or local disk write must not kill the old run.
staging="$(mktemp "$job/.bootstrap.XXXXXXXX")"
trap 'if [[ -n "$staging" ]]; then rm -f -- "$staging"; fi' EXIT
printf '%s' '{{encodedWorker}}' | base64 --decode > "$staging"
chmod 700 "$staging"

if [[ -n "$active" && '{{restartFlag}}' == '1' ]]; then
    # Only stop the exact detached run started by ThunderClient.
    # Never kill an untracked ComfyUI, unrelated PID or the SSH session.
    pgid="$(ps -o pgid= -p "$active" || true)"
    sid="$(ps -o sid= -p "$active" || true)"
    pgid="${pgid//[[:space:]]/}"
    sid="${sid//[[:space:]]/}"
    if [[ "$pgid" != "$active" || "$sid" != "$active" ]] ||
       ! grep -azFq "THUNDER_CLIENT_RUN_ID=$active_run" "/proc/$active/environ"; then
        echo '[THUNDER-CLIENT] Prozessgruppe nicht eindeutig; Neustart aus Sicherheitsgründen gesperrt'
        exit 45
    fi
    echo '[THUNDER-CLIENT] Stoppe den bisherigen Bootstrap/ComfyUI-Prozess kontrolliert'
    kill -TERM -- "-$active" || {
        echo '[THUNDER-CLIENT] Vorheriger Prozess konnte nicht beendet werden'
        exit 45
    }
    for ((i=0; i<45; i++)); do
        if ! kill -0 -- "-$active" 2>/dev/null; then break; fi
        sleep 1
    done
    if kill -0 -- "-$active" 2>/dev/null; then
        echo '[THUNDER-CLIENT] Bisherige Prozessgruppe läuft noch; kein zweiter Bootstrap'
        exit 45
    fi
    echo '[THUNDER-CLIENT] Vorherige Prozessgruppe beendet; Modelle und Workspace bleiben erhalten'
    active=''
fi
if [[ -n "$active" ]]; then
    if [[ ! -f "$job/commit" || ! -f "$job/workflows" ]] ||
       [[ "$(cat "$job/commit")" != '{{revision}}' ]] ||
       [[ "$(cat "$job/workflows")" != '{{workflows}}' ]]; then
        echo '[THUNDER-CLIENT] Anderer Bootstrap bereits aktiv; kein zweiter Start'
        exit 43
    fi
    echo '[THUNDER-CLIENT] Bestehenden Remote-Job wieder aufnehmen'
    flock -u 8
    exit 0
fi
if [[ -f "$job/output.log" && -f "$job/run_id" ]]; then
    old_run="$(cat "$job/run_id")"
    if [[ "$old_run" =~ ^[a-f0-9]{32}$ ]]; then
        mv -- "$job/output.log" "$job/output-$old_run.log"
    else
        echo '[THUNDER-CLIENT] Ungültige vorige Run-ID'
        exit 41
    fi
fi
mv -- "$staging" "$job/bootstrap.sh"
staging=''
touch "$job/output.log"
chmod 600 "$job/output.log"
printf '%s' '{{nextRun}}' > "$job/run_id"
printf '%s' '{{revision}}' > "$job/commit"
printf '%s' '{{workflows}}' > "$job/workflows"
chmod 600 "$job/run_id" "$job/commit" "$job/workflows"

# Secrets are supplied only through the authenticated SSH stdin and inherited
# environment; they are not written to the reviewed script or job metadata.
export GITHUB_TOKEN={{SshService.QuoteShell(githubToken)}}
export HF_TOKEN={{SshService.QuoteShell(huggingFace ?? "")}}
export CIVITAI_TOKEN={{SshService.QuoteShell(civitai ?? "")}}
export WORKFLOWS={{SshService.QuoteShell(workflows)}}
export REPO_REF={{SshService.QuoteShell(revision)}}
export MODEL_DOWNLOAD_WORKERS=4
export THUNDER_CLIENT_RUN_ID={{SshService.QuoteShell(nextRun)}}
nohup setsid bash -c 'exec 8>&-; exec bash "$1"' _ "$job/bootstrap.sh" \
    >>"$job/output.log" 2>&1 </dev/null &
pid=$!
printf '%s' "$pid" > "$job/pid"
chmod 600 "$job/pid"
flock -u 8
printf '[THUNDER-CLIENT] Remote-Job gestartet: %s\n' '{{nextRun}}'
""".Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static string BuildFollowScript() => """
set -Eeuo pipefail
job="$HOME/.thunderclient-deployment"
if [[ ! -f "$job/pid" || ! -f "$job/output.log" || ! -f "$job/run_id" ]]; then
    echo '[THUNDER-CLIENT] Remote-Job oder Log fehlt'
    exit 44
fi
pid="$(cat "$job/pid")"
run="$(cat "$job/run_id")"
if [[ ! "$pid" =~ ^[0-9]+$ || ! "$run" =~ ^[a-f0-9]{32}$ ]]; then
    echo '[THUNDER-CLIENT] Ungültige Remote-Job-Identität'
    exit 44
fi
if [[ -r "/proc/$pid/environ" ]] &&
   grep -azFq "THUNDER_CLIENT_RUN_ID=$run" "/proc/$pid/environ"; then
    exec tail -n +1 -f --pid="$pid" "$job/output.log"
else
    cat "$job/output.log"
fi
""".Replace("\r\n", "\n", StringComparison.Ordinal);
}
