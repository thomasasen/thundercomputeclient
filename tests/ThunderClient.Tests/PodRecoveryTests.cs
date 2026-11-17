using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class PodRecoveryTests
{
    private static InstanceItem Instance(string status = "RUNNING", string uuid = "stable-uuid",
        string id = "0") => new(id, uuid, "existing", status, "a6000",
            "192.0.2.50", 2222, DateTimeOffset.UtcNow, 155, 1);

    private sealed class FakeRunner(params CommandResult[] responses) : ICommandRunner
    {
        public List<string[]> Calls { get; } = [];
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("tnr", executable);
            Calls.Add(arguments.ToArray());
            return Task.FromResult(responses[Calls.Count - 1]);
        }
    }

    private static string LiveList(string status = "RUNNING", string uuid = "stable-uuid") =>
        "[{\"id\":\"0\",\"uuid\":\"" + uuid +
        "\",\"name\":\"existing\",\"status\":\"" + status +
        "\",\"gpuType\":\"a6000\",\"ip\":\"192.0.2.50\",\"port\":2222}]";

    [Fact]
    public void SnapshotNameIsUniqueSafeAndBounded()
    {
        var longName = Instance(id: new string('a', 64));
        var name = PodRecoveryPlanner.RecoverySnapshotName(longName,
            new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero), "bcd123");
        Assert.Equal("recovery-aaaaaaaaaaaa-20261118000000-bcd123", name);
        Assert.True(name.Length <= 50);
        Assert.Matches("^[a-zA-Z0-9_.-]{3,50}$", name);
        Assert.Throws<ArgumentException>(() =>
            PodRecoveryPlanner.RecoverySnapshotName(Instance(), DateTimeOffset.UtcNow, "unsafe!"));
    }

    [Fact]
    public async Task RecoveryOnlyCallsStatusThenSnapshotForSameRunningInstance()
    {
        var runner = new FakeRunner(new CommandResult(0, LiveList(), ""),
            new CommandResult(0, "{}", ""));
        var name = "recovery-0-20261118000000-bcd123";
        await new ThunderCli(runner).RequestRecoverySnapshotAsync(Instance(), name);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(["status", "--json", "--no-wait"], runner.Calls[0]);
        Assert.Equal(["snapshot", "create", "--instance-id", "0", "--name", name, "--json"],
            runner.Calls[1]);
        Assert.DoesNotContain(runner.Calls,
            call => call[0] is "create" or "delete" or "modify" or "restart" or "reboot");
    }

    [Theory]
    [InlineData("CREATING", "stable-uuid")]
    [InlineData("DELETING", "stable-uuid")]
    [InlineData("RUNNING", "different-uuid")]
    public async Task ChangedStateOrUuidBlocksSnapshotWithNoMutation(
        string state, string uuid)
    {
        var runner = new FakeRunner(new CommandResult(0, LiveList(state, uuid), ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ThunderCli(runner).RequestRecoverySnapshotAsync(Instance(),
                "recovery-0-20261118000000-bcd123"));
        Assert.Single(runner.Calls);
        Assert.Equal("status", runner.Calls[0][0]);
    }

    [Fact]
    public async Task MissingInstanceBlocksSnapshotAndDoesNotCreateAnotherPod()
    {
        var runner = new FakeRunner(new CommandResult(0, "[]", ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ThunderCli(runner).RequestRecoverySnapshotAsync(Instance(), "recovery-0-aabbcc"));
        Assert.Single(runner.Calls);
    }

    [Fact]
    public void SnapshotRecoveryIsNotAnInPlaceRestart()
    {
        Assert.Equal("https://console.thundercompute.com/", PodRecoveryPlanner.ConsoleUrl);
        Assert.Equal("0", PodRecoveryPlanner.RequireSameRunningInstance(
            Instance(), [Instance()]).Id);
    }
}
