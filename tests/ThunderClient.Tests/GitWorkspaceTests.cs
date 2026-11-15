using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class GitWorkspaceTests
{
    private sealed class FailedCloneRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            var args = arguments.ToArray();
            Assert.Equal("git", executable);
            Assert.Equal("clone", args[0]);
            var destination = args[^1];
            var pack = Path.Combine(destination, ".git", "objects", "pack");
            Directory.CreateDirectory(pack);
            var index = Path.Combine(pack, "pack-test.idx");
            File.WriteAllText(index, "partial");
            File.SetAttributes(index, FileAttributes.ReadOnly);
            return Task.FromResult(new CommandResult(128, "", "fatal: mocked Git login rejected"));
        }
    }

    private sealed class NeverCalledRunner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Git must not run while another client holds the lock");
    }

    [Fact]
    public async Task FailedCloneKeepsOriginalErrorEvenIfPackIndexCannotBeDeleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "thunder-git-" + Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "workspace");
        try
        {
            var error = await Assert.ThrowsAsync<ExternalCommandException>(() =>
                new GitWorkspace(new FailedCloneRunner()).SynchronizeAsync(destination));
            Assert.Contains("mocked Git login rejected", error.Message);
            Assert.False(Directory.Exists(destination));
        }
        finally { DeleteTestTree(root); }
    }

    [Fact]
    public async Task OtherClientLockPreventsConcurrentSyncWithoutCallingGit()
    {
        var root = Path.Combine(Path.GetTempPath(), "thunder-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var destination = Path.Combine(root, "workspace");
        try
        {
            using (var held = new FileStream(destination + ".sync.lock",
                       FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var error = await Assert.ThrowsAsync<IOException>(() =>
                    new GitWorkspace(new NeverCalledRunner()).SynchronizeAsync(destination));
                Assert.Contains("anderen Client-Fenster", error.Message);
            }
        }
        finally { DeleteTestTree(root); }
    }

    private static void DeleteTestTree(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }
}
