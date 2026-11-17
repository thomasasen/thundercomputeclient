using System.Text.Json;
using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class ThunderConnectDiagnosticsTests
{
    private sealed class ConnectRunner(CommandResult result) : ICommandRunner
    {
        public List<string[]> Calls { get; } = [];
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("tnr", executable);
            Calls.Add(arguments.ToArray());
            return Task.FromResult(result);
        }
    }

    [Fact]
    public void CliErrorInStdoutIsPreferredOverUnrelatedUsageText()
    {
        var result = new CommandResult(1,
            """{"error":"failed to add SSH key: permission denied"}""",
            "Fetching instances...\nChecking SSH keys...\nUsage: tnr connect [instance_id] [flags]\n");
        var diagnostic = ThunderCli.ExplainSshConnectionFailure(result);
        Assert.Contains("failed to add SSH key: permission denied", diagnostic);
        Assert.Contains("Exitcode 1", diagnostic);
        Assert.DoesNotContain("Usage:", diagnostic);
    }

    [Fact]
    public void ExactAddKeyApiTimeoutIsRecognizedWithoutUnrelatedPortGuess()
    {
        var result = new CommandResult(1,
            JsonSerializer.Serialize(new { error =
                "failed to add SSH key: transport error: failed to make request: " +
                "Post \"https://api.thundercompute.com:8443/v1/instances/0/add_key\": " +
                "context deadline exceeded" }),
            "Fetching instances...\nChecking SSH keys...\nUsage: tnr connect [instance_id]");
        Assert.True(ThunderCli.IsSshKeyRegistrationTimeout(result));
        var text = ThunderCli.ExplainSshConnectionFailure(result);
        Assert.Contains("Zustand dieser Anfrage ist unbekannt", text);
        Assert.Contains("NICHT automatisch wiederholt", text);
        Assert.DoesNotContain("Usage:", text);
        Assert.DoesNotContain("SSH-Port ist falsch", text);
    }

    [Theory]
    [InlineData("failed to add SSH key: permission denied")]
    [InlineData("failed to add SSH key: request returned 403")]
    [InlineData("connect failed: context deadline exceeded")]
    [InlineData("failed to add SSH key: /add_key failed with bad request")]
    public void OtherErrorsDoNotMasqueradeAsApiDeadline(string reason)
    {
        var json = JsonSerializer.Serialize(new { error = reason });
        Assert.False(ThunderCli.IsSshKeyRegistrationTimeout(
            new CommandResult(1, json, "")));
    }

    [Fact]
    public async Task ApiTimeoutDoesNotAutomaticallyRetryNonIdempotentKeyRegistration()
    {
        var json = JsonSerializer.Serialize(new { error =
            "failed to add SSH key: transport error: Post " +
            "https://api.thundercompute.com:8443/v1/instances/0/add_key: " +
            "context deadline exceeded" });
        var runner = new ConnectRunner(new CommandResult(1, json, ""));
        var err = await Assert.ThrowsAsync<ThunderSshKeyApiTimeoutException>(() =>
            new ThunderCli(runner).GetSshAsync(
                new InstanceItem("0", "uuid-1", "demo", "RUNNING",
                    "l40", "203.0.113.1", 22444, null)));
        Assert.Contains("Kein neuer Pod ist nötig", err.Message);
        Assert.Single(runner.Calls);
        Assert.Equal(["connect", "0", "--json"], runner.Calls[0]);
    }

    [Fact]
    public void UnknownCliErrorDoesNotInventMissingPortAsRootCause()
    {
        var result = new CommandResult(1, "",
            "Fetching instances...\nChecking SSH keys...\nUsage: tnr connect\n");
        var diagnostic = ThunderCli.ExplainSshConnectionFailure(result);
        Assert.Contains("Der genaue Fehler ist daraus nicht bestimmbar", diagnostic);
        Assert.DoesNotContain("Port ist falsch", diagnostic);
    }

    [Fact]
    public void CliErrorDoesNotEchoTokenFromJson()
    {
        var result = new CommandResult(1,
            """{"error":"Bearer ghp_secretfake1234567890 was rejected"}""", "");
        var diagnostic = ThunderCli.ExplainSshConnectionFailure(result);
        Assert.DoesNotContain("ghp_secretfake1234567890", diagnostic);
        Assert.Contains("[TOKEN MASKIERT]", diagnostic);
    }

    [Fact]
    public async Task NumericInstanceIdZeroAndProviderSshPortAreAccepted()
    {
        var temp = Path.Combine(Path.GetTempPath(), "thunder-key-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(temp, "dummy test material (not a private key)");
        try
        {
            var json = JsonSerializer.Serialize(new {
                ip = "203.0.113.1", port = 22444, key_file = temp
            });
            var runner = new ConnectRunner(new CommandResult(0, json, "Checking SSH keys..."));
            var connection = await new ThunderCli(runner).GetSshAsync(
                new InstanceItem("0", "uuid-1", "demo", "RUNNING", "l40",
                    "203.0.113.1", 22444, null));
            Assert.Equal("0", connection.InstanceId);
            Assert.Equal(22444, connection.Port);
            Assert.Equal(["connect", "0", "--json"], Assert.Single(runner.Calls));
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public async Task CliErrorIsRaisedBeforeReadingIncompleteConnection()
    {
        var runner = new ConnectRunner(new CommandResult(1,
            """{"error":"failed to add SSH key"}""",
            "Checking SSH keys...\nUsage: tnr connect"));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ThunderCli(runner).GetSshAsync(
                new InstanceItem("0", "uuid-1", "demo", "RUNNING", "l40",
                    "203.0.113.1", 22444, null)));
        Assert.Contains("failed to add SSH key", failure.Message);
        Assert.DoesNotContain("Usage: tnr", failure.Message);
    }

    [Fact]
    public void PortZeroIsExplicitlyNotReadyRatherThanPretendingSshUsesPort22()
    {
        var item = new InstanceItem("0", "uuid-1", "demo", "RUNNING", "l40",
            "203.0.113.1", 0, null);
        Assert.Equal("Noch nicht bereit", item.SshPortDisplay);
        Assert.Equal("22444", (item with { Port = 22444 }).SshPortDisplay);
    }
}
