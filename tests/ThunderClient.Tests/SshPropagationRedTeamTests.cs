using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class SshPropagationRedTeamTests
{
    private sealed class FakeRunner(params CommandResult[] responses) : ICommandRunner
    {
        public readonly List<(string Executable, string[] Args)> Calls = [];
        private readonly Queue<CommandResult> _responses = new(responses);
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((executable, arguments.ToArray()));
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private static SshConnection Endpoint() =>
        new("0", "203.0.113.1", 40002, "/fake/private-key");

    [Fact]
    public async Task TransientPublicKeyPropagationRetriesOnlyReadOnlySshTrue()
    {
        var fake = new FakeRunner(
            new CommandResult(255, "", "Permission denied (publickey)"),
            new CommandResult(255, "", "Permission denied (publickey)"),
            new CommandResult(0, "", ""));
        var statuses = new List<string>();
        var result = await new SshService(fake).WaitForAuthenticationAsync(
            Endpoint(), statuses.Add, attempts: 3,
            delay: (_, _) => Task.CompletedTask);
        Assert.True(result.Success);
        Assert.Equal(3, fake.Calls.Count);
        Assert.Equal(2, statuses.Count);
        Assert.All(fake.Calls, call => {
            Assert.Equal("ssh", call.Executable);
            Assert.Contains("ubuntu@203.0.113.1", call.Args);
            Assert.Equal("true", call.Args.Last());
            Assert.Contains("StrictHostKeyChecking=yes", call.Args);
            Assert.DoesNotContain("connect", call.Args);
        });
    }

    [Fact]
    public async Task HostKeyTamperingMustNotBeRetriedOrAutoTrusted()
    {
        var fake = new FakeRunner(
            new CommandResult(255, "", "REMOTE HOST IDENTIFICATION HAS CHANGED"));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SshService(fake).WaitForAuthenticationAsync(Endpoint(),
                attempts: 6, delay: (_, _) => Task.CompletedTask));
        Assert.Contains("Hostfingerprint", exception.Message);
        Assert.Single(fake.Calls);
    }

    [Fact]
    public async Task BrokenSshStopsAfterSmallBoundedNumberOfReadOnlyAttempts()
    {
        var fake = new FakeRunner(
            new CommandResult(255, "", "Permission denied (publickey)"),
            new CommandResult(255, "", "Permission denied (publickey)"));
        var result = await new SshService(fake).WaitForAuthenticationAsync(
            Endpoint(), attempts: 2, delay: (_, _) => Task.CompletedTask);
        Assert.False(result.Success);
        Assert.Equal(2, fake.Calls.Count);
    }
}
