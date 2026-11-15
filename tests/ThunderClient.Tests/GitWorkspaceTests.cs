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

    [Fact]
    public void SparseCheckoutNeverIncludesWindowsInvalidIncomingTree()
    {
        Assert.Contains("workflows", GitWorkspace.SparseDirectories);
        Assert.Contains("thunder", GitWorkspace.SparseDirectories);
        Assert.Contains("scripts", GitWorkspace.SparseDirectories);
        Assert.DoesNotContain("incoming", GitWorkspace.SparseDirectories);
    }

    private sealed class SparseCloneRunner : ICommandRunner
    {
        public List<string[]> Calls { get; } = [];

        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("git", executable);
            var args = arguments.ToArray();
            Calls.Add(args);
            if (args[0] == "clone")
            {
                Assert.Contains("--no-checkout", args);
                var folder = args[^1];
                Directory.CreateDirectory(Path.Combine(folder, ".git"));
            }
            else if (args.Contains("sparse-checkout"))
            {
                Assert.Equal(["-C", args[1], "sparse-checkout", "set", "--cone",
                    "workflows", "thunder", "scripts", "docs"], args);
            }
            else if (args.Contains("checkout"))
            {
                var folder = args[1];
                Directory.CreateDirectory(Path.Combine(folder, "workflows"));
                Directory.CreateDirectory(Path.Combine(folder, "thunder"));
                File.WriteAllText(Path.Combine(folder, "thunder", "bootstrap.sh"), "#!/bin/bash");
            }
            else if (args.Contains("rev-parse"))
            {
                return Task.FromResult(new CommandResult(0, new string('a', 40), ""));
            }
            else throw new InvalidOperationException("Unexpected git call");
            return Task.FromResult(new CommandResult(0, "", ""));
        }
    }

    [Fact]
    public async Task FreshCloneUsesSparseCheckoutBeforeAnyMaterialization()
    {
        var root = Path.Combine(Path.GetTempPath(), "thunder-git-" + Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "workspace");
        var runner = new SparseCloneRunner();
        try
        {
            var sync = await new GitWorkspace(runner).SynchronizeAsync(destination);
            Assert.StartsWith("aaaaaaaa", sync.Commit);
            Assert.True(File.Exists(Path.Combine(destination, "thunder", "bootstrap.sh")));
            Assert.Equal(4, runner.Calls.Count);
            Assert.Equal("clone", runner.Calls[0][0]);
            Assert.Contains("sparse-checkout", runner.Calls[1]);
            Assert.Contains("checkout", runner.Calls[2]);
            Assert.Contains("rev-parse", runner.Calls[3]);
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
