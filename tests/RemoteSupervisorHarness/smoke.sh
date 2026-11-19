#!/usr/bin/env bash
set -Eeuo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
scratch="$(mktemp -d)"
trap 'if [[ -f "$HOME/.thunderclient-deployment/pid" ]]; then
        kill "$(cat "$HOME/.thunderclient-deployment/pid")" 2>/dev/null || true
      fi
      rm -rf "$scratch"' EXIT
export HOME="$scratch/home"
mkdir -p "$HOME"
dotnet run --project "$root/tests/RemoteSupervisorHarness/RemoteSupervisorHarness.csproj" \
    --configuration Release -- launch > "$scratch/launch.sh"
dotnet run --project "$root/tests/RemoteSupervisorHarness/RemoteSupervisorHarness.csproj" \
    --configuration Release -- follow > "$scratch/follow.sh"
dotnet run --project "$root/tests/RemoteSupervisorHarness/RemoteSupervisorHarness.csproj" \
    --configuration Release -- restart > "$scratch/restart.sh"
bash -n "$scratch/launch.sh"
bash -n "$scratch/follow.sh"
bash -n "$scratch/restart.sh"

# The launch SSH script returns immediately; the detached worker remains.
bash "$scratch/launch.sh" > "$scratch/first.txt"
grep -q 'Remote-Job gestartet' "$scratch/first.txt"
job="$HOME/.thunderclient-deployment"
[[ -f "$job/pid" && -f "$job/output.log" && -f "$job/run_id" ]]
run="$(cat "$job/run_id")"
pid="$(cat "$job/pid")"
[[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid"
# Repeating launch with the same chosen workflow must not fork another worker.
bash "$scratch/launch.sh" > "$scratch/second.txt"
grep -q 'Bestehenden Remote-Job wieder aufnehmen' "$scratch/second.txt"
[[ "$(cat "$job/run_id")" == "$run" ]]
[[ "$(cat "$job/pid")" == "$pid" ]]
timeout 15 bash "$scratch/follow.sh" > "$scratch/followed.txt"
grep -q '\[TEST-REMOTE\] started' "$scratch/followed.txt"
grep -q '\[TEST-REMOTE\] completed' "$scratch/followed.txt"
! grep -q 'gh-demo' "$job/bootstrap.sh"
! grep -q 'gh-demo' "$job/output.log"
# Simulate an expensive persistent model file. A software restart must
# never erase this directory or trigger destructive Pod infrastructure calls.
mkdir -p "$HOME/comfyui-workspace/ComfyUI/models/checkpoints"
echo 'persistent-model' > "$HOME/comfyui-workspace/ComfyUI/models/checkpoints/keep.safetensors"
bash "$scratch/launch.sh" > "$scratch/third.txt"
grep -q 'Remote-Job gestartet' "$scratch/third.txt"
prior_pid="$(cat "$job/pid")"
prior_run="$(cat "$job/run_id")"
kill -0 "$prior_pid"
bash "$scratch/restart.sh" > "$scratch/restarted.txt"
grep -q 'Stoppe den bisherigen Bootstrap/ComfyUI-Prozess kontrolliert' "$scratch/restarted.txt"
grep -q 'Vorherige Prozessgruppe beendet' "$scratch/restarted.txt"
grep -q 'Remote-Job gestartet' "$scratch/restarted.txt"
[[ "$(cat "$job/run_id")" != "$prior_run" ]]
[[ "$(cat "$job/pid")" != "$prior_pid" ]]
[[ "$(cat "$HOME/comfyui-workspace/ComfyUI/models/checkpoints/keep.safetensors")" == 'persistent-model' ]]
timeout 15 bash "$scratch/follow.sh" > "$scratch/restart-followed.txt"
grep -q '\[TEST-REMOTE\] completed' "$scratch/restart-followed.txt"
! grep -q 'gh-demo' "$job/bootstrap.sh"
echo 'REMOTE SUPERVISOR SMOKE OK: detached, reattached, terminated tracked process, kept persistent model, no token on disk'
