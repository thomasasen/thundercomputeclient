using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

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
        SshConnection endpoint, CancellationToken ct = default)
    {
        const int attempts = 3;
        string lastDiagnostic = "keine Ausgabe";
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            CommandResult scan;
            try
            {
                scan = await commands.RunAsync("ssh-keyscan",
                    ["-T", "8", "-p", endpoint.Port.ToString(), "-t", "ed25519", endpoint.Host],
                    TimeSpan.FromSeconds(15), cancellationToken: ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastDiagnostic = "ssh-keyscan hat das Zeitlimit überschritten.";
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

            lastDiagnostic = "ssh-keyscan Exitcode " + scan.ExitCode +
                "; stderr: " + SafeScanMessage(scan.Stderr);
            if (attempt < attempts) await Task.Delay(1500, ct);
        }
        throw new InvalidOperationException(
            "SSH-Hostschlüssel konnte nach " + attempts + " Versuchen nicht abgefragt werden " +
            "(Port " + endpoint.Port + ", nur ED25519). " + lastDiagnostic +
            " Der SSH-Dienst des Pods kann noch starten, der Netzwerkport ist eventuell nicht erreichbar " +
            "oder der Server bietet keinen ED25519-Schlüssel an. " +
            "Pod-Status prüfen und später erneut verbinden. " +
            "Die Hostschlüsselprüfung bleibt aktiv; keinen unbekannten Fingerprint ungeprüft bestätigen.");
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
        if (keyBytes.Length is < 40 or > 1024)
            throw new InvalidDataException("Unerwartete ED25519-Hostschlüssellänge.");
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

    public async Task<CommandResult> BootstrapAsync(SshConnection endpoint, string adapter,
        string workflows, string gitToken, string? huggingFace, string? civitai,
        Action<string> onLine, CancellationToken ct = default)
    {
        if (!adapter.StartsWith("#!/usr/bin/env bash\n", StringComparison.Ordinal))
            throw new InvalidDataException("Thunder-Adapter enthält keinen erwarteten Bash-Header.");
        if (string.IsNullOrWhiteSpace(gitToken)) throw new InvalidOperationException("GitHub-Token für den Remote-Bootstrap fehlt.");
        var script = "set -e\numask 077\n" +
            "command -v flock >/dev/null || { echo '[THUNDER-CLIENT] flock fehlt'; exit 41; }\n" +
            "exec 9>\"$HOME/.thunderclient-bootstrap.lock\"\n" +
            "if ! flock -n 9; then echo '[THUNDER-CLIENT] Ein Bootstrap läuft bereits'; exit 42; fi\n" +
            "export GITHUB_TOKEN=" + QuoteShell(gitToken) + "\n" +
            "export HF_TOKEN=" + QuoteShell(huggingFace ?? "") + "\n" +
            "export CIVITAI_TOKEN=" + QuoteShell(civitai ?? "") + "\n" +
            "export WORKFLOWS=" + QuoteShell(workflows) + "\n" +
            "export REPO_REF=main\n" + adapter + "\n";
        return await RunScriptAsync(endpoint, script, onLine, ct);
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

