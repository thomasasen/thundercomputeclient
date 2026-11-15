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

    private string[] CommonArgs(SshConnection endpoint) =>
    [
        "-i", endpoint.KeyFile, "-p", endpoint.Port.ToString(),
        "-o", "IdentitiesOnly=yes", "-o", "BatchMode=yes",
        "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=" + _knownHosts,
        "-o", "ConnectTimeout=15"
    ];

    public async Task<(string Fingerprint, string KeyLine)> ScanHostKeyAsync(
        SshConnection endpoint, CancellationToken ct = default)
    {
        var scan = await commands.RunAsync("ssh-keyscan",
            ["-T", "10", "-p", endpoint.Port.ToString(), "-t", "ed25519", endpoint.Host],
            TimeSpan.FromSeconds(20), cancellationToken: ct);
        var line = scan.Stdout.Split('\n').Select(x => x.Trim()).FirstOrDefault(x =>
            !x.StartsWith('#') && x.Contains("ssh-ed25519", StringComparison.Ordinal));
        if (line is null) throw new InvalidOperationException("SSH-Hostschlüssel konnte nicht abgefragt werden.");
        var components = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length < 3 || components[1] != "ssh-ed25519")
            throw new InvalidDataException("Unerwartetes SSH-Keyscan-Format.");
        var hash = SHA256.HashData(Convert.FromBase64String(components[2]));
        var fingerprint = "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
        return (fingerprint, line);
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
        start.ArgumentList.Add("ssh");
        foreach (var arg in args) start.ArgumentList.Add(arg);
        try { Process.Start(start); }
        catch (Win32Exception)
        {
            // cmd.exe starts an interactive SSH console when Windows Terminal is unavailable.
            var fallback = new ProcessStartInfo("cmd.exe") { UseShellExecute = true };
            fallback.ArgumentList.Add("/k");
            fallback.ArgumentList.Add("ssh " + string.Join(" ", args.Select(QuoteWindows)));
            Process.Start(fallback);
        }
    }

    private static string QuoteWindows(string text) =>
        "\"" + text.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    public void StartTunnel(SshConnection endpoint)
    {
        if (_tunnel is { HasExited: false }) return;
        var start = new ProcessStartInfo("ssh") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in CommonArgs(endpoint)) start.ArgumentList.Add(arg);
        foreach (var arg in new[] { "-N", "-L", "127.0.0.1:8188:127.0.0.1:8188", $"ubuntu@{endpoint.Host}" })
            start.ArgumentList.Add(arg);
        _tunnel = Process.Start(start) ?? throw new InvalidOperationException("SSH-Tunnel konnte nicht gestartet werden.");
    }

    public void StopTunnel()
    {
        if (_tunnel is null) return;
        try { if (!_tunnel.HasExited) _tunnel.Kill(entireProcessTree: true); }
        finally { _tunnel.Dispose(); _tunnel = null; }
    }
}

