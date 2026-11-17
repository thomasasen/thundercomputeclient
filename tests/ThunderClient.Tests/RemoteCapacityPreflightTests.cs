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
    public async Task Nominal48GbGpuWithEccIsNotRejectedAs47Or45GiB()
    {
        // A nominal 48 GB card may report only 46068 MiB with ECC enabled.
        // 46068 MiB = 48.30 decimal GB, but only 44.99 binary GiB.
        const int reportedMiB = 46068;
        Assert.True(reportedMiB * 1048576d / 1e9 >= 48);
        Assert.True(reportedMiB / 1024d < 48);

        var runner = new FakeRunner(new CommandResult(0,
            "[THUNDER-PREFLIGHT] free_gb=230.0 required_gb=116 gpu_count=1 vram_gb=48.30 required_vram_gb=48", ""));
        var output = await new SshService(runner)
            .CheckGpuAndFreeDiskAsync(Endpoint(), 1, 48, 95.574);

        Assert.Contains("vram_gb=48.30", output);
        Assert.Contains("int(line.strip()) * (1024 ** 2) / 1e9", runner.Input);
        Assert.DoesNotContain("int(line.strip()) / 1024", runner.Input);
        Assert.Contains("required_vram_gb={vram_required}", runner.Input);
    }

    [Fact]
    public async Task LowVramShowsMeasuredCapacityAndReason()
    {
        var runner = new FakeRunner(new CommandResult(46,
            "[THUNDER-PREFLIGHT] free_gb=230.0 required_gb=116 gpu_count=1 vram_gb=46.14 required_vram_gb=48\n" +
            "[THUNDER-PREFLIGHT] blocked reason=gpu-vram-insufficient", ""));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SshService(runner).CheckGpuAndFreeDiskAsync(Endpoint(), 1, 48, 95.574));
        Assert.Contains("vram_gb=46.14", error.Message);
        Assert.Contains("required_vram_gb=48", error.Message);
        Assert.Contains("gpu-vram-insufficient", error.Message);
        Assert.Contains("Keine großen Modelldownloads", error.Message);
    }

    [Fact]
    public async Task DifferentiateInsufficientGpuCountFromInsufficientVram()
    {
        var runner = new FakeRunner(new CommandResult(46,
            "[THUNDER-PREFLIGHT] free_gb=230.0 required_gb=116 gpu_count=1 vram_gb=48.30 required_vram_gb=48\n" +
            "[THUNDER-PREFLIGHT] blocked reason=gpu-count-insufficient", ""));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SshService(runner).CheckGpuAndFreeDiskAsync(Endpoint(), 2, 48, 95.574));
        Assert.Contains("gpu-count-insufficient", error.Message);
        Assert.Contains("if len(vram) < expected:", runner.Input);
    }

    [Fact]
    public async Task MissingCapacityReportFailsClosed()
    {
        var runner = new FakeRunner(new CommandResult(0, "", ""));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SshService(runner).CheckGpuAndFreeDiskAsync(Endpoint(), 1, 48, 95.574));
        Assert.Contains("keinen Kapazitätsbericht", error.Message);
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
