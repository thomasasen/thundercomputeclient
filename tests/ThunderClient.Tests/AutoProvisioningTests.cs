using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class AutoProvisioningTests
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly WorkflowHardwareReference Vram48 = new(133,
        [new ReferenceGpu(1, "L40", 48, "Referenzklasse")]);
    private static WorkflowItem Item(string name, double? size,
        WorkflowHardwareReference? reference = null, string? source = null) => new()
    {
        Name = name, Family = name, Description = "A selected private workflow",
        CanvasFiles = ["workflow.json"],
        ReadmeUrl = "https://github.com/example/private",
        HardwareReference = reference ?? Vram48,
        Models = [new ModelWeight("hf", "checkpoints", name + ".bin",
            source ?? name + "/model", size, false)]
    };

    private static GpuChoice[] Gpus() => [
        new("a6000", "RTX A6000", 48, 6, 0.35m),
        new("l40", "L40", 48, 6, 0.79m),
        new("a100", "A100", 80, 8, 1.09m) { Enabled = false }
    ];

    [Fact]
    public void ComputesDiskFromManifestButNeverShrinksUserSetting()
    {
        var plan = AutoProvisioning.Plan([Item("demo", 108.78)],
            Commit, Gpus(), 1, 160, "base", 0.85m);
        Assert.Equal(160, plan.DiskGb);
        Assert.Equal(108.78, plan.ModelGb, 2);
        Assert.Equal(["a6000", "l40"], plan.Candidates.Select(x => x.Key).ToArray());
        Assert.Equal(48, plan.ReferenceVramGb);
        Assert.Equal("demo", Assert.Single(plan.WorkflowNames));
        Assert.Contains("keine Thunder-Livemessung", plan.Warning);
    }

    [Fact]
    public void AutomaticallyRaisesDiskOnlyWithKnownManifestSizes()
    {
        var plan = AutoProvisioning.Plan([Item("huge", 188.0)],
            Commit, Gpus(), 1, 100, "base", 0.85m);
        Assert.Equal(228, plan.DiskGb);
    }

    [Fact]
    public void NeverEnablesUncheckedOrPriceIneligibleGpu()
    {
        var gpus = Gpus();
        gpus[0].Enabled = false;
        var plan = AutoProvisioning.Plan([Item("selected", 6.0)],
            Commit, gpus, 1, 100, "base", .82m);
        Assert.Equal("l40", Assert.Single(plan.Candidates).Key);
        Assert.False(gpus[0].Enabled);
        Assert.False(gpus[2].Enabled);
    }

    [Fact]
    public void RefusesHighVramIfOnlyDisabledCandidateMeetsRequirement()
    {
        var gpu = new WorkflowHardwareReference(150,
            [new ReferenceGpu(1, "H100", 80, "groß")]);
        var error = Assert.Throws<InvalidOperationException>(() =>
            AutoProvisioning.Plan([Item("video", 120, gpu)],
                Commit, Gpus(), 1, 160, "base", 2m));
        Assert.Contains("GPU", error.Message);
        Assert.Contains("Schätzpreisgrenze", error.Message);
    }

    [Fact]
    public void PreventsL40WithExtraVcpusFromPassingStalePriceCeiling()
    {
        var l40 = new GpuChoice("l40", "L40", 48, 12, .79m);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AutoProvisioning.Plan([Item("demo", 12)], Commit,
                [l40], 1, 149, "base", .85m));
        Assert.Contains("Schätzpreisgrenze", exception.Message);
    }

    [Fact]
    public void RefusesUnknownModelSizeBeforeCostlyPodCreation()
    {
        var err = Assert.Throws<InvalidOperationException>(() =>
            AutoProvisioning.Plan([Item("unknown", null)], Commit,
                Gpus(), 1, 160, "base", .85m));
        Assert.Contains("Modelldateigrößen", err.Message);
    }

    [Fact]
    public void RejectsConflictingModelsAndUntrustedCatalogCommit()
    {
        var a = Item("one", 5, source: "org/a");
        var b = Item("two", 5, source: "org/b");
        b = new WorkflowItem {
            Name = b.Name, Family = b.Family, Description = b.Description,
            CanvasFiles = b.CanvasFiles, ReadmeUrl = b.ReadmeUrl,
            HardwareReference = b.HardwareReference,
            Models = [new ModelWeight("hf", "checkpoints", "one.bin", "other/source", 5, false)]
        };
        Assert.Throws<InvalidOperationException>(() =>
            AutoProvisioning.Plan([a,b], Commit, Gpus(), 1, 160, "base", .85m));
        Assert.Throws<InvalidOperationException>(() =>
            AutoProvisioning.Plan([a], "main", Gpus(), 1, 160, "base", .85m));
    }

    private static InstanceItem Pod(string status, int port, string ip = "203.0.113.9") =>
        new("0", "uuid", "pod", status, "l40", ip, port, null);

    [Fact]
    public void AdditionalAuthorizationCannotBeSilentlyGrantedByAutomation()
    {
        var restricted = new WorkflowItem {
            Name = "restricted", Family = "restricted", Description = "test",
            ReadmeUrl = "https://github.com/private", CanvasFiles = ["workflow.json"],
            Models = [new ModelWeight("hf", "checkpoints", "restricted.bin", "org/repo", 10, false)],
            RequiredAuthorizationEnv = "USER_ACKNOWLEDGED_LICENSE"
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            AutoProvisioning.Plan([restricted], Commit, Gpus(), 1, 160, "base", .85m));
        Assert.Contains("Autorisierungsbestätigung", error.Message);
    }

    [Fact]
    public async Task WaitsForRealSshEndpointWithoutCreatingOrProvisioningAnything()
    {
        var statuses = new IReadOnlyList<InstanceItem>[]
        {
            [Pod("BUILDING", 0, "")],
            [Pod("RUNNING", 0)],
            [Pod("RUNNING", 22022)]
        };
        var calls = 0;
        var delays = 0;
        var logs = new List<string>();
        var found = await AutoProvisioning.WaitForRunningSshAsync("0",
            _ => Task.FromResult(statuses[Math.Min(calls++, 2)]),
            (_, _) => { delays++; return Task.CompletedTask; }, logs.Add,
            maxChecks: 5);
        Assert.Equal("0", found.Id);
        Assert.Equal(22022, found.Port);
        Assert.Equal(3, calls);
        Assert.Equal(2, delays);
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task MissingPodBlocksAnyBlindRecreation()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AutoProvisioning.WaitForRunningSshAsync("0",
                _ => Task.FromResult<IReadOnlyList<InstanceItem>>([]),
                (_, _) => Task.CompletedTask, maxChecks: 1));
        Assert.Contains("Keine neue Instanz", error.Message);
    }

    [Fact]
    public async Task TimeoutWarnsThatExistingPodMayKeepCharging()
    {
        var delayCount = 0;
        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            AutoProvisioning.WaitForRunningSshAsync("0",
                _ => Task.FromResult<IReadOnlyList<InstanceItem>>([Pod("STARTING", 0)]),
                (_, _) => { delayCount++; return Task.CompletedTask; }, maxChecks: 3));
        Assert.Equal(2, delayCount);
        Assert.Contains("Kosten", error.Message);
    }

    [Fact]
    public async Task CancelledPollingDoesNotProduceAdditionalCreate()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AutoProvisioning.WaitForRunningSshAsync("0",
                _ => throw new InvalidOperationException("Should never call Thunder after cancel"),
                (_, _) => Task.CompletedTask,
                ct: cts.Token));
    }
}
