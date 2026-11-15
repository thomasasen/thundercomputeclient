namespace ThunderClient.Core;

public sealed record SyncResult(string Commit, bool Offline, string Detail);
public sealed class GitWorkspace(ICommandRunner commands)
{
    public const string Repository = "https://github.com/thomasasen/runpod-ai-workflows.git";

    // The private upstream has Windows-incompatible names under incoming/.
    // The GUI needs only workflow manifests and the Thunder bootstrap adapter.
    // Keep the root README/start.sh via cone-mode sparse checkout, while
    // deliberately never materializing the incoming/ tree on Windows.
    public static IReadOnlyList<string> SparseDirectories { get; } =
        ["workflows", "thunder", "scripts", "docs"];

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
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        // Prevent two running clients from cloning/fetching the same checkout simultaneously.
        // The lock lives next to the checkout; it does not dirty the Git worktree.
        using var workspaceLock = AcquireWorkspaceLock(full);
        if (Directory.Exists(full) && !Directory.Exists(Path.Combine(full, ".git")))
            throw new InvalidOperationException("Workflow-Verzeichnis existiert, enthält aber keinen Git-Checkout. Keine Daten werden überschrieben.");

        if (!Directory.Exists(full))
        {
            var temp = full + ".clone-" + Guid.NewGuid().ToString("N");
            try
            {
                await CheckedAsync(["clone", "--no-checkout", "--single-branch", "--branch", "main", Repository, temp],
                    TimeSpan.FromMinutes(10), ct);
                await CheckedAsync(["-C", temp, "sparse-checkout", "set", "--cone", .. SparseDirectories],
                    TimeSpan.FromMinutes(3), ct);
                await CheckedAsync(["-C", temp, "checkout", "main"],
                    TimeSpan.FromMinutes(3), ct);
                ValidateRequiredFiles(temp);
                Directory.Move(temp, full);
            }
            catch
            {
                // Windows/antivirus can lock or mark an index/pack file read-only.
                // Cleanup must NEVER replace the original Git error with
                // 'Access to the path pack-*.idx is denied'.
                try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
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
            // Migrate any pre-existing full checkout safely after ensuring that
            // there are no local changes; future fetch/merge stays sparse.
            await CheckedAsync(Args(["sparse-checkout", "set", "--cone", .. SparseDirectories]),
                TimeSpan.FromMinutes(3), ct);
            await CheckedAsync(Args("fetch", "--no-tags", "origin", "main"), TimeSpan.FromMinutes(5), ct);
            var ancestor = await GitAsync(Args("merge-base", "--is-ancestor", "HEAD", "FETCH_HEAD"), ct: ct);
            if (ancestor.ExitCode != 0)
                throw new InvalidOperationException("Lokaler Git-Stand ist nicht per Fast-Forward aktualisierbar.");
            await CheckedAsync(Args("merge", "--ff-only", "FETCH_HEAD"), ct: ct);
            ValidateRequiredFiles(full);
        }
        return new SyncResult(await CurrentCommitAsync(full, ct), false, "GitHub main synchronisiert");
    }

    private static void ValidateRequiredFiles(string checkout)
    {
        if (!Directory.Exists(Path.Combine(checkout, "workflows")) ||
            !File.Exists(Path.Combine(checkout, "thunder", "bootstrap.sh")))
            throw new InvalidDataException(
                "Workflow-Checkout unvollständig: workflows/ oder thunder/bootstrap.sh fehlt. " +
                "Kein unvollständiger Git-Stand wird übernommen.");
    }

    private static FileStream AcquireWorkspaceLock(string checkout)
    {
        try
        {
            return new FileStream(checkout + ".sync.lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new IOException(
                "Die Workflow-Synchronisierung läuft möglicherweise bereits in einem anderen Client-Fenster. " +
                "Bitte nur eine Instanz gleichzeitig aktualisieren.", ex);
        }
    }

    public async Task<string> CurrentCommitAsync(string folder, CancellationToken ct = default)
    {
        if (!Directory.Exists(Path.Combine(folder, ".git")))
            throw new DirectoryNotFoundException("Kein gültiger lokaler Git-Checkout.");
        return await CheckedAsync(["-C", folder, "rev-parse", "HEAD"], ct: ct);
    }
}

