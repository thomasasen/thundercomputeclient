using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public class DeploymentReadinessGateTests
{
    private const string Commit = "b5276af36dd0ccd4477f49de86e4a120a8f8ac54";

    private static DeploymentReadinessGate NewGate() => new(
        [
            new ModelWeight("hf", "checkpoints", "first.safetensors", "hf:first", 2, false),
            new ModelWeight("civitai", "loras", "second.safetensors", "civitai:2", 1, false)
        ], Commit);

    private static void FinishPrerequisites(DeploymentReadinessGate gate)
    {
        gate.Observe("[POD-BOOTSTRAP] GitHub-Commit: " + Commit + " WORKFLOWS: demo");
        foreach (var phase in new[] { "custom_nodes", "workflow_copies", "workflow_setups",
                                      "dependency_final_audit", "cuda_handoff_preflight" })
            gate.Observe("[START-TIMING] phase=" + phase + " elapsed_s=2.000 status=ok");
        gate.Observe("[DEPENDENCY-AUDIT] final=1 pip_check=ok pip_issue_lines=0 checked=35 missing=0 conflict=0 unresolved=0");
        gate.Observe("Download-Abschluss: Alle ausgewählten Modelle vorhanden oder erfolgreich geladen");
        gate.Observe("[START-TIMING] phase=comfyui_http_ready elapsed_s=15.000 status=ok");
    }

    [Fact]
    public void HttpReadyAloneDoesNotAuthorizeFullReadiness()
    {
        var gate = NewGate();
        gate.Observe("[START-TIMING] phase=comfyui_http_ready elapsed_s=5.000 status=ok");
        Assert.True(gate.HttpReady);
        Assert.False(gate.ReadyForAudit);
        Assert.Equal(0, gate.FinishedModels);
    }

    [Fact]
    public void ExactModelAndPhaseCompletionOpensOnlyIndependentAudit()
    {
        var gate = NewGate();
        FinishPrerequisites(gate);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "hf:checkpoints/first.safetensors" """);
        gate.Observe("""[DOWNLOAD-STATUS] VORHANDEN "civitai:loras/second.safetensors" """);
        Assert.Equal(2, gate.ExpectedModels);
        Assert.Equal(2, gate.FinishedModels);
        Assert.True(gate.ReadyForAudit);
        Assert.False(gate.HasBlocker);
    }

    [Fact]
    public void MissingModelNeverBecomesReadyEvenWithHttp200()
    {
        var gate = NewGate();
        FinishPrerequisites(gate);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "hf:checkpoints/first.safetensors" """);
        Assert.False(gate.ReadyForAudit);
        Assert.Equal(1, gate.FinishedModels);
    }

    [Fact]
    public void FailedModelStaysBlocked()
    {
        var gate = NewGate();
        FinishPrerequisites(gate);
        gate.Observe("""[DOWNLOAD-STATUS] FEHLER "civitai:loras/second.safetensors" """);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "civitai:loras/second.safetensors" """);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "hf:checkpoints/first.safetensors" """);
        Assert.True(gate.HasBlocker);
        Assert.False(gate.ReadyForAudit);
    }

    [Fact]
    public void DifferentRemoteCommitIsNotReady()
    {
        var gate = NewGate();
        FinishPrerequisites(gate);
        gate.Observe("[POD-BOOTSTRAP] GitHub-Commit: " + new string('a', 40) + " WORKFLOWS: demo");
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "hf:checkpoints/first.safetensors" """);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "civitai:loras/second.safetensors" """);
        Assert.Contains("anderen GitHub-Commit", gate.Blocker);
        Assert.False(gate.ReadyForAudit);
    }

    [Theory]
    [InlineData("[DEPENDENCY-AUDIT] final=1 pip_check=warning pip_issue_lines=2 checked=20 missing=0 conflict=0 unresolved=0")]
    [InlineData("[DEPENDENCY-AUDIT] final=1 pip_check=ok pip_issue_lines=0 checked=20 missing=1 conflict=0 unresolved=0")]
    [InlineData("[DEPENDENCY-AUDIT] final=1 pip_check=ok pip_issue_lines=0 checked=20 missing=0 conflict=0 unresolved=2")]
    public void DependencyWarningsBlockFullReadiness(string audit)
    {
        var gate = NewGate();
        FinishPrerequisites(gate);
        gate.Observe(audit);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "hf:checkpoints/first.safetensors" """);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "civitai:loras/second.safetensors" """);
        Assert.True(gate.HasBlocker);
        Assert.False(gate.ReadyForAudit);
    }

    [Fact]
    public void UnexpectedModelBlocksReadiness()
    {
        var gate = NewGate();
        FinishPrerequisites(gate);
        gate.Observe("""[DOWNLOAD-STATUS] GELADEN "hf:checkpoints/other.safetensors" """);
        Assert.True(gate.HasBlocker);
    }
}
