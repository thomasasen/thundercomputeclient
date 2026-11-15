namespace ThunderClient.Core;

public sealed record SyncResult(string Commit, bool Offline, string Detail);
public sealed class GitWorkspace(ICommandRunner commands)
{
    public const string Repository = "https://github.com/thomasasen/runpod-ai-workflows.git";

    private Task<CommandResult> GitAsync(IEnumerable<string> args, TimeSpan? timeout = null,
        CancellationToken ct = default)
        => commands.RunAsync("git", args, timeout ?? TimeSpan.FromMinutes(3), cancellationToken: ct);

    private async Task<string> CheckedAsync(IEnumerable<string> args, TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var result = await GitAsync(args, timeout, ct);
        if (!result.Success) throw new ExternalCommandException("git", result);
        return result.Stdout.Trim();
    }

    public async Task<SyncResult> SynchronizeAsync(string destination, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(destination);
        if (Directory.Exists(full) && !Directory.Exists(Path.Combine(full, ".git")))
            throw new InvalidOperationException("Workflow-Verzeichnis existiert, enthält aber keinen Git-Checkout. Keine Daten werden überschrieben.");

        if (!Directory.Exists(full))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var temp = full + ".clone-" + Guid.NewGuid().ToString("N");
            try
            {
                await CheckedAsync(["clone", "--single-branch", "--branch", "main", Repository, temp],
                    TimeSpan.FromMinutes(10), ct);
                Directory.Move(temp, full);
            }
            finally { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
        }
        else
        {
            var prefix = new[] { "-C", full };
            string[] Args(params string[] tail) => prefix.Concat(tail).ToArray();
            var origin = await CheckedAsync(Args("remote", "get-url", "origin"), ct: ct);
            var validOrigin = origin.Equals(Repository, StringComparison.OrdinalIgnoreCase) ||
                              origin.Equals(Repository[..^4], StringComparison.OrdinalIgnoreCase) ||
                              origin.Equals("git@github.com:thomasasen/runpod-ai-workflows.git", StringComparison.OrdinalIgnoreCase);
            if (!validOrigin) throw new InvalidOperationException("Git-Origin stimmt nicht mit der erlaubten Workflow-Quelle überein.");
            var branch = await CheckedAsync(Args("branch", "--show-current"), ct: ct);
            if (branch != "main") throw new InvalidOperationException("Checkout ist nicht auf main; Synchronisierung gestoppt.");
            var changed = await CheckedAsync(Args("status", "--porcelain", "--untracked-files=normal"), ct: ct);
            if (!string.IsNullOrWhiteSpace(changed))
                throw new InvalidOperationException("Lokale Änderungen vorhanden. Kein Überschreiben oder automatischer Merge.");
            await CheckedAsync(Args("fetch", "--no-tags", "origin", "main"), TimeSpan.FromMinutes(5), ct);
            var ancestor = await GitAsync(Args("merge-base", "--is-ancestor", "HEAD", "FETCH_HEAD"), ct: ct);
            if (ancestor.ExitCode != 0)
                throw new InvalidOperationException("Lokaler Git-Stand ist nicht per Fast-Forward aktualisierbar.");
            await CheckedAsync(Args("merge", "--ff-only", "FETCH_HEAD"), ct: ct);
        }
        return new SyncResult(await CurrentCommitAsync(full, ct), false, "GitHub main synchronisiert");
    }

    public async Task<string> CurrentCommitAsync(string folder, CancellationToken ct = default)
    {
        if (!Directory.Exists(Path.Combine(folder, ".git")))
            throw new DirectoryNotFoundException("Kein gültiger lokaler Git-Checkout.");
        return await CheckedAsync(["-C", folder, "rev-parse", "HEAD"], ct: ct);
    }
}

