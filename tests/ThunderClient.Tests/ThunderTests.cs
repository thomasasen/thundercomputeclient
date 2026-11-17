using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public class ThunderTests
{
    private sealed class FakeRunner : ICommandRunner
    {
        public Queue<CommandResult> Responses { get; } = new();
        public List<string[]> Calls { get; } = [];
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(arguments.ToArray());
            return Task.FromResult(Responses.Dequeue());
        }
    }

    private static CommandResult Ok(string json) => new(0, json, "");
    private static CommandResult Fail(string text) => new(1, "", text);
    private static readonly string OneInstance =
        """[{"id":"123","uuid":"uuid-1","name":"demo","status":"RUNNING","gpuType":"l40","ip":"127.0.0.2","port":2200,"createdAt":"2026-11-15T14:00:00Z"}]""";
    private static string LockFolder() => Path.Combine(Path.GetTempPath(), "thunder-lock-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CapacityErrorFallsBackOnlyWhenNoInstanceExists()
    {
        var fake = new FakeRunner();
        fake.Responses.Enqueue(Ok("[]"));
        fake.Responses.Enqueue(Fail("GPU configuration a6000 x1 is currently unavailable"));
        fake.Responses.Enqueue(Ok("[]"));
        fake.Responses.Enqueue(Ok("""{"identifier":123,"uuid":"uuid-1"}"""));
        fake.Responses.Enqueue(Ok(OneInstance));
        var client = new ThunderCli(fake);
        var choices = new[]
        {
            new GpuChoice("a6000", "RTX A6000", 48, 6, .35m),
            new GpuChoice("l40", "L40", 48, 6, .79m)
        };
        var folder = LockFolder();
        try
        {
            var instance = await client.CreateWithFallbackAsync(choices, 1, 160, "base", 1m, folder, _ => { });
            Assert.Equal("123", instance.Id);
            var creates = fake.Calls.Where(x => x[0] == "create").ToArray();
            Assert.Equal(2, creates.Length);
            Assert.Equal("a6000", creates[0][Array.IndexOf(creates[0], "--gpu") + 1]);
            Assert.Equal("l40", creates[1][Array.IndexOf(creates[1], "--gpu") + 1]);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task UnknownErrorPreventsSecondCreation()
    {
        var fake = new FakeRunner();
        fake.Responses.Enqueue(Ok("[]"));
        fake.Responses.Enqueue(Fail("payment required"));
        fake.Responses.Enqueue(Ok("[]"));
        var client = new ThunderCli(fake);
        var folder = LockFolder();
        try
        {
            await Assert.ThrowsAsync<ExternalCommandException>(() =>
                client.CreateWithFallbackAsync([
                    new GpuChoice("a6000", "RTX", 48, 6, .35m),
                    new GpuChoice("l40", "L40", 48, 6, .79m)
                ], 1, 160, "base", 2m, folder, _ => { }));
            Assert.Single(fake.Calls.Where(x => x[0] == "create"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task ExistingInstancePreventsAnyCreation()
    {
        var fake = new FakeRunner();
        fake.Responses.Enqueue(Ok(OneInstance));
        var client = new ThunderCli(fake);
        var folder = LockFolder();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.CreateWithFallbackAsync([
                    new GpuChoice("a6000", "RTX", 48, 6, .35m)
                ], 1, 160, "base", 2m, folder, _ => { }));
            Assert.DoesNotContain(fake.Calls, x => x[0] == "create");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("payment required")]
    [InlineData("timeout")]
    [InlineData("authentication failed")]
    [InlineData("generic network unavailable")]
    public void FallbackRejectsAllNonCapacityErrors(string message) =>
        Assert.False(ThunderCli.IsCertainCapacityFailure(message));

    [Fact]
    public void CreateSuccessKeepsExactIdentityWhenStatusIsEventuallyConsistent()
    {
        var identity = ThunderCli.ParseCreateIdentity(
            """{"identifier":123,"uuid":"uuid-123","key":"SECRET-DO-NOT-LOG"}""");
        Assert.Equal("123", identity.Id);
        Assert.Equal("uuid-123", identity.Uuid);
        Assert.Throws<FormatException>(() =>
            ThunderCli.ParseCreateIdentity("""{"key":"SECRET-DO-NOT-LOG"}"""));
    }

    [Fact]
    public async Task CreateSuccessReturnsPinnedIdentityWhenStatusIsEmpty()
    {
        var fake = new FakeRunner();
        fake.Responses.Enqueue(Ok("[]"));  // before create
        fake.Responses.Enqueue(Ok("""{"identifier":123,"uuid":"uuid-123","key":"PRIVATE"}"""));
        fake.Responses.Enqueue(Ok("[]"));  // status cache has not caught up yet
        var client = new ThunderCli(fake);
        var folder = LockFolder();
        var logs = new List<string>();
        try
        {
            var instance = await client.CreateWithFallbackAsync(
                [new GpuChoice("a6000","RTX A6000",48,6,.35m)],
                1, 149, "base", .85m, folder, logs.Add);
            Assert.Equal("123", instance.Id);
            Assert.Equal("uuid-123", instance.Uuid);
            Assert.Equal("CREATING", instance.Status);
            Assert.DoesNotContain("PRIVATE", string.Join("\n", logs));
            Assert.Single(fake.Calls.Where(x => x[0] == "create"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task CreateStatusFailureStillReturnsNewPaidPodIdentity()
    {
        var fake = new FakeRunner();
        fake.Responses.Enqueue(Ok("[]"));
        fake.Responses.Enqueue(Ok("""{"identifier":123,"uuid":"uuid-123","key":"SENSITIVE"}"""));
        fake.Responses.Enqueue(Fail("Status API unavailable"));
        var directory = LockFolder();
        var log = new List<string>();
        try
        {
            var created = await new ThunderCli(fake).CreateWithFallbackAsync(
                [new GpuChoice("a6000", "RTX", 48, 6, .35m)],
                1, 120, "base", .85m, directory, log.Add);
            Assert.Equal("123", created.Id);
            Assert.Equal("CREATING", created.Status);
            Assert.DoesNotContain("SENSITIVE", string.Join("\n", log));
            Assert.Single(fake.Calls.Where(call => call[0] == "create"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void BudgetIncludesExtraVcpusAndDoesNotTrustEditedPriceBelowPublishedMinimum()
    {
        var l40 = new GpuChoice("l40", "L40", 48, 12, .10m);
        var rate = Estimates.Hourly(l40, 1, 149);
        Assert.InRange(rate, 1.04m, 1.05m);
        Assert.True(rate > .85m);
        Assert.Throws<InvalidOperationException>(() =>
            Estimates.Hourly(new GpuChoice("unknown", "Unknown", 48, 6, .01m), 1, 100));
    }

    [Fact]
    public void BudgetIncludesExtraDiskAndIsOnlyAnEstimate()
    {
        var a6000 = new GpuChoice("a6000", "RTX", 48, 6, .35m);
        var rate = Estimates.Hourly(a6000, 1, 135);
        Assert.True(rate > .35m);
        Assert.True(rate < .4m);
        Assert.Contains("WARNUNG", Estimates.CapacityWarning(108.78, 135));
    }

    [Fact]
    public void StatusRequiresJsonArray()
    {
        Assert.Throws<FormatException>(() => ThunderCli.ParseInstances("{}"));
        Assert.Empty(ThunderCli.ParseInstances("[]"));
    }

    [Fact]
    public void ShellQuotingPreventsCommandInjection()
    {
        Assert.Equal("'a'\\'';echo unsafe'", SshService.QuoteShell("a';echo unsafe"));
    }
}

