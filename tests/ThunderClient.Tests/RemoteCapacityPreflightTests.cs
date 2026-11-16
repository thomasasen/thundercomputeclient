using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class RemoteCapacityPreflightTests
{
    private sealed class FakeRunner(CommandResult result) : ICommandRunner
    {
        public string? Input;
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> args,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Input = standardInput;
            return Task.FromResult(result);
        }
    }

    private static SshConnection Endpoint() => new("0", "203.0.113.5", 22022, "/ssh/key");

    [Fact]
    public async Task ChecksRealDiskGpuCountAndVramBeforeAnyDownload()
    {
        var runner = new FakeRunner(new CommandResult(0,
            "[THUNDER-PREFLIGHT] free_gb=230.0 required_gb=110 gpu_count=1 vram_gb=48.0", ""));
        var service = new SshService(runner);
        var output = await service.CheckGpuAndFreeDiskAsync(Endpoint(), 1, 48, 90);
        Assert.Contains("free_gb=230.0", output);
        Assert.Contains("nvidia-smi", runner.Input);
        Assert.Contains("shutil.disk_usage", runner.Input);
        Assert.Contains("required = 110", runner.Input);
        Assert.DoesNotContain("GITHUB_TOKEN", runner.Input);
    }

    [Fact]
    public async Task LowDiskRefusesBootstrap()
    {
        var runner = new FakeRunner(new CommandResult(47,
            "[THUNDER-PREFLIGHT] blocked reason=free-disk-insufficient", ""));
        var service = new SshService(runner);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CheckGpuAndFreeDiskAsync(Endpoint(), 1, 48, 90));
        Assert.Contains("free-disk-insufficient", exception.Message);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1)]
    public async Task InvalidSizesFailBeforeRemoteExecution(double size)
    {
        var runner = new FakeRunner(new CommandResult(0,"", ""));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new SshService(runner).CheckGpuAndFreeDiskAsync(Endpoint(), 1, 48, size));
        Assert.Null(runner.Input);
    }
}
