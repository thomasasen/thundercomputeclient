using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed class SshService(ICommandRunner commands)
{
    private readonly string _knownHosts = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThunderClient", "known_hosts");
    private Process? _tunnel;
    private string? _tunnelIdentity;
    public bool TunnelIsRunning => _tunnel is { HasExited: false };

    private string[] CommonArgs(SshConnection endpoint) =>
    [
        "-i", endpoint.KeyFile, "-p", endpoint.Port.ToString(),
        "-o", "IdentitiesOnly=yes", "-o", "BatchMode=yes",
        "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=" + _knownHosts,
        "-o", "ConnectTimeout=15"
    ];

    // The first SSH connection can race with the VM's sshd boot. A bounded
    // retry is permitted for discovery only; it never auto-trusts a host key.
    public async Task<(string Fingerprint, string KeyLine)> ScanHostKeyAsync(
        SshConnection endpoint, CancellationToken ct = default,
        Action<string>? progress = null)
    {
        const int attempts = 3;
        string lastDiagnostic = "keine Ausgabe";
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Invoke("SSH-Hostschlüssel: Abfrage " + attempt + "/" + attempts +
                " über ssh-keyscan (maximal 15 Sekunden pro Versuch).");
            CommandResult scan;
            try
            {
                scan = await commands.RunAsync("ssh-keyscan",
                    ["-T", "8", "-p", endpoint.Port.ToString(), "-t", "ed25519", endpoint.Host],
                    TimeSpan.FromSeconds(15), cancellationToken: ct);
            }
            catch (TimeoutException)
            {
                lastDiagnostic = "ssh-keyscan hat nach 15 Sekunden nicht geantwortet.";
                progress?.Invoke(lastDiagnostic);
                if (attempt < attempts) await Task.Delay(1500, ct);
                continue;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Compatibility with runners which expose internal deadline
                // cancellation directly instead of a TimeoutException.
                lastDiagnostic = "ssh-keyscan hat das Zeitlimit überschritten.";
                progress?.Invoke(lastDiagnostic);
                if (attempt < attempts) await Task.Delay(1500, ct);
                continue;
            }
            // OpenSSH may return a nonzero exit code while emitting one valid
            // key from several attempts. Only the exact host/port and ED25519
            // format are accepted.
            var line = scan.Stdout.Split('\n').Select(x => x.Trim())
                .FirstOrDefault(x => !x.StartsWith('#') &&
                    x.Contains("ssh-ed25519", StringComparison.Ordinal));
            if (line is not null)
                return ParseEd25519HostKey(endpoint, line);

            // Win32-OpenSSH 9.2/9.5 ssh-keyscan advertises a sntrup KEX
            // that its own Windows binary cannot negotiate (upstream #2140).
            // Unlike ssh-keyscan, ssh.exe filters unavailable algorithms.
            // Capture an ED25519 key into a disposable file using ssh.exe;
            // this file is NEVER used for subsequent authenticated sessions.
            if (IsBrokenWindowsKeyscanKex(scan.Stderr))
            {
                progress?.Invoke("Windows-OpenSSH-KEX-Fehler erkannt. " +
                    "Lese den öffentlichen Hostschlüssel mit ssh.exe, ohne Anmeldung " +
                    "und ohne Änderung der vertrauenswürdigen Hostschlüssel.");
                try { return await ScanHostKeyViaIsolatedSshAsync(endpoint, ct, progress); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (ex is InvalidOperationException or
                    IOException or TimeoutException or OperationCanceledException)
                {
                    throw new InvalidOperationException(
                        "Windows-OpenSSH ssh-keyscan kann den vom Pod angebotenen " +
                        "Schlüsselaustausch nicht verarbeiten (bekannter Windows-Fehler). " +
                        "Die automatische Abfrage mit ssh.exe in einer isolierten, " +
                        "nur temporären known_hosts-Datei ist ebenfalls gescheitert: " +
                        ex.Message +
                        " Pod NICHT erneut erstellen. Unter Systemstatus die " +
                        "OpenSSH-Installation prüfen; alternativ eine aktuelle " +
                        "kompatible OpenSSH-Version verwenden. " +
                        "Der echte SSH-Hostschlüssel wird niemals automatisch vertraut.", ex);
                }
            }
            lastDiagnostic = "ssh-keyscan Exitcode " + scan.ExitCode +
                "; stderr: " + SafeScanMessage(scan.Stderr);
            if (attempt < attempts) await Task.Delay(1500, ct);
        }
        throw new InvalidOperationException(
            "SSH-Hostschlüssel konnte nach " + attempts + " Versuchen nicht abgefragt werden " +
            "(Host " + endpoint.Host + ", Port " + endpoint.Port + ", nur ED25519). " + lastDiagnostic +
            " Der SSH-Dienst des Pods kann noch starten, der Netzwerkport ist eventuell nicht erreichbar " +
            "oder der Server bietet keinen ED25519-Schlüssel an. " +
            "Pod-Status prüfen und später erneut verbinden. " +
            "Die Hostschlüsselprüfung bleibt aktiv; keinen unbekannten Fingerprint ungeprüft bestätigen.");
    }

    public static bool IsBrokenWindowsKeyscanKex(string diagnostic)
        => diagnostic.Contains("choose_kex: unsupported KEX method", StringComparison.OrdinalIgnoreCase) &&
           diagnostic.Contains("sntrup761x25519-sha512@openssh.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Work around Win32-OpenSSH ssh-keyscan #2140 without reducing host
    /// authentication security. A normal ssh client negotiates the host key
    /// into an isolated disposable known_hosts file. accept-new is permitted
    /// ONLY for collecting an untrusted public key in that temporary file.
    /// No user credentials, private key, real known_hosts or shell command
    /// are sent. The key still requires out-of-band user approval and is
    /// subsequently used with StrictHostKeyChecking=yes.
    /// </summary>
    private async Task<(string Fingerprint, string KeyLine)> ScanHostKeyViaIsolatedSshAsync(
        SshConnection endpoint, CancellationToken ct, Action<string>? progress)
    {
        if (!System.Net.IPAddress.TryParse(endpoint.Host, out _) ||
            endpoint.Port is < 1 or > 65535)
            throw new InvalidOperationException("Ungültige SSH-IP oder Portnummer für die Hostschlüsselabfrage.");

        var isolatedDir = Path.Combine(Path.GetTempPath(),
            "ThunderClient-HostKey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolatedDir);
        var untrustedKnownHosts = Path.Combine(isolatedDir, "untrusted_known_hosts");
        try
        {
            var args = new[]
            {
                "-F", "none", // ignore the user's ssh config and ProxyCommand
                "-p", endpoint.Port.ToString(),
                "-o", "HostKeyAlgorithms=ssh-ed25519",
                "-o", "StrictHostKeyChecking=accept-new",
                "-o", "UserKnownHostsFile=" + untrustedKnownHosts,
                "-o", "GlobalKnownHostsFile=NUL",
                "-o", "HashKnownHosts=no",
                "-o", "UpdateHostKeys=no",
                "-o", "CheckHostIP=no",
                "-o", "BatchMode=yes",
                "-o", "ConnectTimeout=10",
                "-o", "ConnectionAttempts=1",
                "-o", "NumberOfPasswordPrompts=0",
                "-o", "PreferredAuthentications=none",
                "-o", "PubkeyAuthentication=no",
                "-o", "PasswordAuthentication=no",
                "-o", "KbdInteractiveAuthentication=no",
                "-o", "HostbasedAuthentication=no",
                "-o", "GSSAPIAuthentication=no",
                "-N", // no remote command is ever executed
                "ubuntu@" + endpoint.Host
            };
            CommandResult handshake;
            try
            {
                handshake = await commands.RunAsync("ssh", args, TimeSpan.FromSeconds(18),
                    cancellationToken: ct);
            }
            catch (TimeoutException)
            {
                // The isolated known_hosts file may already contain a valid
                // ED25519 public key, even if ssh -N outlives its deadline.
                progress?.Invoke("SSH-Handshake-Zeitlimit. " +
                    "Prüfe, ob der öffentliche Schlüssel bereits erfasst wurde.");
                handshake = new CommandResult(255, "", "SSH-Handshake-Zeitlimit.");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                progress?.Invoke("SSH-Handshake wurde nach internem Zeitlimit beendet. " +
                    "Prüfe, ob ein öffentlicher Schlüssel vorliegt.");
                handshake = new CommandResult(255, "", "SSH-Handshake-Zeitlimit.");
            }
            if (!File.Exists(untrustedKnownHosts))
                throw new InvalidOperationException(
                    "SSH hat keine ED25519-Hostschlüsseldatei geliefert. " +
                    SafeScanMessage(handshake.Stderr));

            // Do not trust a successful process exit: only an exactly matched
            // host:port + cryptographically well-formed ED25519 blob is usable.
            var expectedHost = endpoint.Port == 22
                ? endpoint.Host : "[" + endpoint.Host + "]:" + endpoint.Port;
            var lines = await File.ReadAllLinesAsync(untrustedKnownHosts, ct);
            var candidates = lines.Where(line =>
                line.StartsWith(expectedHost + " ssh-ed25519 ", StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException(
                    "Der temporäre SSH-Handshake lieferte keine eindeutige " +
                    "ED25519-Hostschlüsselzeile für diesen Pod.");
            progress?.Invoke("Öffentlicher ED25519-Hostschlüssel erfasst. " +
                "Vor dem Vertrauensaufbau ist eine unabhängige Fingerprint-Prüfung notwendig.");
            return ParseEd25519HostKey(endpoint, candidates[0]);
        }
        finally
        {
            // Only ephemeral, untrusted public material was ever stored here.
            // This is NOT the application's approved known_hosts file.
            try { Directory.Delete(isolatedDir, recursive: true); }
            catch (IOException) { /* best effort on cancellation */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    public static (string Fingerprint, string KeyLine) ParseEd25519HostKey(
        SshConnection endpoint, string line)
    {
        if (line.Contains('\r') || line.Contains('\n'))
            throw new InvalidDataException("SSH-Keyscan enthält mehrere oder ungültige Zeilen.");
        var components = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expectedHost = endpoint.Port == 22
            ? endpoint.Host : "[" + endpoint.Host + "]:" + endpoint.Port;
        if (components.Length < 3 || components[0] != expectedHost ||
            components[1] != "ssh-ed25519")
            throw new InvalidDataException(
                "SSH-Keyscan lieferte keinen ED25519-Schlüssel für den erwarteten Host und Port.");
        byte[] keyBytes;
        try { keyBytes = Convert.FromBase64String(components[2]); }
        catch (FormatException ex)
        {
            throw new InvalidDataException("SSH-Hostschlüssel ist kein gültiger Base64-Schlüssel.", ex);
        }
        if (keyBytes.Length != 51 ||
            BinaryPrimitives.ReadInt32BigEndian(keyBytes.AsSpan(0, 4)) != 11 ||
            Encoding.ASCII.GetString(keyBytes, 4, 11) != "ssh-ed25519" ||
            BinaryPrimitives.ReadInt32BigEndian(keyBytes.AsSpan(15, 4)) != 32)
            throw new InvalidDataException("Unerwartetes ED25519-Hostschlüsselformat.");
        var hash = SHA256.HashData(keyBytes);
        var fingerprint = "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
        // Remove untrusted trailing comments from host-key material.
        return (fingerprint, expectedHost + " ssh-ed25519 " + components[2]);
    }

    private static string SafeScanMessage(string stderr)
    {
        var singleLine = new string(stderr.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        return singleLine.Length == 0 ? "(keine Diagnose von ssh-keyscan)" :
            singleLine[..Math.Min(350, singleLine.Length)];
    }

    public async Task<bool> HasKnownHostAsync(SshConnection endpoint, CancellationToken ct = default)
    {
        if (!File.Exists(_knownHosts)) return false;
        var hostField = endpoint.Port == 22 ? endpoint.Host : $"[{endpoint.Host}]:{endpoint.Port}";
        var result = await commands.RunAsync("ssh-keygen",
            ["-F", hostField, "-f", _knownHosts], TimeSpan.FromSeconds(10), cancellationToken: ct);
        return result.Success;
    }

    public void TrustHostKey(string line)
    {
        if (line.Contains('\n') || line.Contains('\r')) throw new ArgumentException("Ungültige Hostschlüsselzeile.");
        Directory.CreateDirectory(Path.GetDirectoryName(_knownHosts)!);
        File.AppendAllText(_knownHosts, line + Environment.NewLine, Encoding.ASCII);
    }

    public async Task<CommandResult> ProbeAuthenticationAsync(
        SshConnection endpoint, CancellationToken ct = default)
    {
        // Only use previously trusted known_hosts; never scan, accept or create keys.
        // Bound runtime so a blocked remote shell does not hang the diagnostics UI.
        var args = CommonArgs(endpoint).Concat(
            ["-o", "NumberOfPasswordPrompts=0", "-T", $"ubuntu@{endpoint.Host}", "true"]);
        return await commands.RunAsync("ssh", args, TimeSpan.FromSeconds(25),
            cancellationToken: ct);
    }

    /// <summary>
    /// The provider may return HTTP 200 for add_key before authorized_keys
    /// has propagated to sshd. Bounded read-only ssh true probes only:
    /// NEVER call /add_key, tnr connect, or mutate trusted host keys here.
    /// </summary>
    public async Task<CommandResult> WaitForAuthenticationAsync(
        SshConnection endpoint, Action<string>? progress = null,
        int attempts = 6, Func<TimeSpan, CancellationToken, Task>? delay = null,
        CancellationToken ct = default)
    {
        if (attempts is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(attempts));
        delay ??= (pause, token) => Task.Delay(pause, token);
        CommandResult last = new(255, "", "Noch kein SSH-Probe ausgeführt");
        for (var i = 0; i < attempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            last = await ProbeAuthenticationAsync(endpoint, ct);
            if (last.Success) return last;
            var error = last.Combined;
            // A changed host key or a missing independent trust entry is a
            // security failure, not a transient authorized_keys delay.
            if (error.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED",
                    StringComparison.OrdinalIgnoreCase) ||
                error.Contains("Host key verification failed",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "SSH-Hostfingerprint nicht mehr gültig. " +
                    "Keine erneute Verbindung ohne unabhängige Hostprüfung.");
            progress?.Invoke("SSH-Key-Propagation noch nicht bestätigt (" +
                (i + 1) + "/" + attempts + "). " +
                "Es wird nur ssh true wiederholt, kein weiterer API-Key-POST.");
            if (i + 1 < attempts)
                await delay(TimeSpan.FromSeconds(5), ct);
        }
        return last;
    }

    public async Task<CommandResult> RunScriptAsync(SshConnection endpoint, string script,
        Action<string>? onLine = null, CancellationToken ct = default)
    {
        var args = CommonArgs(endpoint).Concat(["-T", $"ubuntu@{endpoint.Host}", "bash -s"]);
        return await commands.RunAsync("ssh", args, TimeSpan.FromHours(12), script, onLine, ct);
    }

    public async Task<string> CollectHardwareAsync(SshConnection endpoint, CancellationToken ct = default)
    {
        var script = "set -eu\n" +
            "nvidia-smi --query-gpu=name,memory.used,memory.total,utilization.gpu,temperature.gpu --format=csv,noheader || true\n" +
            "df -h \"$HOME\" || true\nfree -h || true\n";
        var result = await RunScriptAsync(endpoint, script, ct: ct);
        if (!result.Success) throw new ExternalCommandException("ssh Hardware-Prüfung", result);
        return result.Stdout;
    }

    public static string QuoteShell(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    public async Task<string> CheckGpuAndFreeDiskAsync(SshConnection endpoint,
        int expectedGpus, int requiredVramGb, double modelGb,
        CancellationToken ct = default)
    {
        if (expectedGpus < 1 || requiredVramGb < 0 ||
            !double.IsFinite(modelGb) || modelGb < 0)
            throw new ArgumentOutOfRangeException(nameof(modelGb),
                "Ungültige VRAM-/GPU-/Modellkapazitätsanforderung.");
        // Must run before large model transfers. The supplied VRAM threshold
        // is an advisory README planning value, preferably provider-specific;
        // it is intentionally enforced as a conservative auto-deploy policy
        // and is not proof that 8-MP inference will fit. The remote check also
        // confirms real GPU count and enough free disk for all selected models.
        // No untrusted workflow string is inserted into the remote shell.
        var expectedFreeGb = (int)Math.Ceiling(modelGb + 20);
        var script = "python3 - <<'PY'\n" +
            "import pathlib, shutil, subprocess, sys\n" +
            "home = pathlib.Path.home()\n" +
            "workspace = home / 'comfyui-workspace'\n" +
            "free_gb = shutil.disk_usage(workspace if workspace.is_dir() else home).free / 1e9\n" +
            "required = " + expectedFreeGb + "\n" +
            "expected = " + expectedGpus + "\n" +
            "vram_required = " + requiredVramGb + "\n" +
            "try:\n" +
            "    gpu = subprocess.run(['nvidia-smi', '--query-gpu=memory.total', " +
                "'--format=csv,noheader,nounits'], capture_output=True, " +
                "text=True, timeout=15, check=True)\n" +
            "    # nvidia-smi reports MiB; README VRAM classes are decimal GB, not GiB.\n" +
            "    vram = [int(line.strip()) * (1024 ** 2) / 1e9 for line in gpu.stdout.splitlines() " +
                "if line.strip()]\n" +
            "except (OSError, ValueError, subprocess.CalledProcessError, subprocess.TimeoutExpired):\n" +
            "    print('[THUNDER-PREFLIGHT] blocked reason=gpu-unavailable', flush=True)\n" +
            "    sys.exit(45)\n" +
            "print(f'[THUNDER-PREFLIGHT] free_gb={free_gb:.1f} " +
                "required_gb={required} gpu_count={len(vram)} " +
                "vram_gb={min(vram) if vram else 0:.2f} required_vram_gb={vram_required}', flush=True)\n" +
            "if len(vram) < expected:\n" +
            "    print(\'[THUNDER-PREFLIGHT] blocked reason=gpu-count-insufficient\', flush=True)\n" +
            "    sys.exit(46)\n" +
            "if any(value < vram_required for value in vram[:expected]):\n" +
            "    print('[THUNDER-PREFLIGHT] blocked reason=gpu-vram-insufficient', flush=True)\n" +
            "    sys.exit(46)\n" +
            "if free_gb < required:\n" +
            "    print('[THUNDER-PREFLIGHT] blocked reason=free-disk-insufficient', flush=True)\n" +
            "    sys.exit(47)\n" +
            "PY\n";
        var result = await RunScriptAsync(endpoint, script, ct: ct);
        if (!result.Success)
        {
            // Show the actual (non-secret) measured capacity, not just the reason.
            var diagnostics = result.Stdout.Split('\n', StringSplitOptions.TrimEntries)
                .Where(line => line.StartsWith("[THUNDER-PREFLIGHT] ", StringComparison.Ordinal))
                .Take(3)
                .Select(line => line.Length > 250 ? line[..250] : line)
                .ToArray();
            throw new InvalidOperationException(
                "Remote-GPU-/Disk-Vorprüfung fehlgeschlagen: " +
                (diagnostics.Length > 0 ? string.Join("; ", diagnostics) :
                    "SSH/CUDA/Festplatte nicht prüfbar") +
                ". Keine großen Modelldownloads gestartet.");
        }
        // A successful SSH process without a capacity report is not a passed preflight.
        if (!result.Stdout.Split('\n', StringSplitOptions.TrimEntries)
            .Any(line => line.StartsWith("[THUNDER-PREFLIGHT] free_gb=", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "Remote-GPU-/Disk-Vorprüfung lieferte keinen Kapazitätsbericht. " +
                "Keine großen Modelldownloads gestartet.");
        return result.Stdout.Trim();
    }

    public async Task<bool> IsComfyUiReadyAsync(SshConnection endpoint, CancellationToken ct = default)
    {
        // Probe the actual remote ComfyUI HTTP API on loopback, never a public port.
        // Do not confuse a stale open TCP port with a healthy ComfyUI server.
        var script = "python3 - <<'PY'\n" +
            "import urllib.request\n" +
            "try:\n" +
            "    with urllib.request.urlopen('http://127.0.0.1:8188/system_stats', timeout=3) as r:\n" +
            "        if r.status == 200: print('THUNDER_COMFY_READY')\n" +
            "except (OSError, ValueError): pass\n" +
            "PY\n";
        var response = await RunScriptAsync(endpoint, script, ct: ct);
        return response.Success &&
            response.Stdout.Contains("THUNDER_COMFY_READY", StringComparison.Ordinal);
    }

    public async Task<RemoteReadinessResult> VerifyDeploymentReadyAsync(
        SshConnection endpoint, string expectedCommit, string workflows,
        CancellationToken ct = default)
    {
        // Never treat a local HTTP-ready log event as proof of model/node completeness.
        // A separate authenticated SSH session checks the exact pinned manifest
        // and the currently running backend, without modifying the pod.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var script = RemoteReadinessAudit.BuildScript(expectedCommit, workflows);
        var outcome = await RunScriptAsync(endpoint, script, ct: deadline.Token);
        return RemoteReadinessAudit.Parse(outcome);
    }

    public static string PinBootstrapAdapter(string adapter, string commit)
    {
        if (!Regex.IsMatch(commit, @"\A[a-fA-F0-9]{40}\z"))
            throw new InvalidDataException("Ungültiger Remote-Commit-Pin.");
        const string guard = "if [[ \"${REPO_REF:-main}\" != 'main' ]]; then";
        const string export = "export REPO_REF=main";
        const string entry = "contents/scripts/pod_entry.py?ref=main";
        if (!adapter.StartsWith("#!/usr/bin/env bash\n", StringComparison.Ordinal) ||
            adapter.Split(guard).Length != 2 ||
            adapter.Split(export).Length != 2 ||
            adapter.Split(entry).Length != 2)
            throw new InvalidDataException(
                "Thunder-Adapter-Vertrag hat sich geändert. Kein ungesicherter main-Download.");
        var revision = commit.ToLowerInvariant();
        return adapter
            .Replace(guard, "if [[ \"${REPO_REF:-" + revision +
                "}\" != '" + revision + "' ]]; then", StringComparison.Ordinal)
            .Replace(export, "export REPO_REF=" + revision, StringComparison.Ordinal)
            .Replace(entry, "contents/scripts/pod_entry.py?ref=" + revision, StringComparison.Ordinal);
    }

    public async Task<CommandResult> BootstrapAsync(SshConnection endpoint, string adapter,
        string workflows, string expectedCommit, string gitToken,
        string? huggingFace, string? civitai, Action<string> onLine,
        CancellationToken ct = default)
    {
        // Start or safely reattach a persistent job without placing tokens in
        // on-pod scripts, arguments or persistent metadata.
        var launcher = RemoteBootstrapSupervisor.BuildLaunchScript(
            adapter, expectedCommit, workflows, gitToken, huggingFace, civitai);
        var created = await RunScriptAsync(endpoint, launcher, onLine, ct);
        if (!created.Success) return created;
        onLine("[THUNDER-CLIENT] Remote-Job verbunden; Live-Log ab Beginn. " +
            "Das Schließen der Windows-App stoppt den Pod-Job nicht.");
        return await RunScriptAsync(endpoint,
            RemoteBootstrapSupervisor.BuildFollowScript(), onLine, ct);
    }

    public void OpenTerminal(SshConnection endpoint)
    {
        var args = CommonArgs(endpoint).Concat([$"ubuntu@{endpoint.Host}"]).ToArray();
        var start = new ProcessStartInfo("wt.exe") { UseShellExecute = true };
        start.ArgumentList.Add(ProcessRunner.ResolveOpenSshPath("ssh"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        try { Process.Start(start); }
        catch (Win32Exception)
        {
            // cmd.exe starts an interactive SSH console when Windows Terminal is unavailable.
            var fallback = new ProcessStartInfo("cmd.exe") { UseShellExecute = true };
            fallback.ArgumentList.Add("/k");
            fallback.ArgumentList.Add(QuoteWindows(ProcessRunner.ResolveOpenSshPath("ssh")) + " " + string.Join(" ", args.Select(QuoteWindows)));
            Process.Start(fallback);
        }
    }

    private static string QuoteWindows(string text) =>
        "\"" + text.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    public void StartTunnel(SshConnection endpoint)
    {
        var identity = endpoint.Host + ":" + endpoint.Port;
        if (TunnelIsRunning && _tunnelIdentity == identity) return;
        StopTunnel();
        var start = new ProcessStartInfo(ProcessRunner.ResolveOpenSshPath("ssh")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in CommonArgs(endpoint)) start.ArgumentList.Add(arg);
        foreach (var arg in new[] { "-o", "ExitOnForwardFailure=yes", "-N", "-L", "127.0.0.1:8188:127.0.0.1:8188", $"ubuntu@{endpoint.Host}" })
            start.ArgumentList.Add(arg);
        _tunnel = Process.Start(start) ?? throw new InvalidOperationException("SSH-Tunnel konnte nicht gestartet werden.");
        _tunnelIdentity = identity;
    }

    public void StopTunnel()
    {
        if (_tunnel is null) return;
        try { if (!_tunnel.HasExited) _tunnel.Kill(entireProcessTree: true); }
        finally { _tunnel.Dispose(); _tunnel = null; _tunnelIdentity = null; }
    }
}

