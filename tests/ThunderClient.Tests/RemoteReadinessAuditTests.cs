using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public class RemoteReadinessAuditTests
{
    private const string Commit = "b5276af36dd0ccd4477f49de86e4a120a8f8ac54";

    [Fact]
    public void ReadOnlyAuditUsesThePinnedManifestAndLocalComfyUIEndpoints()
    {
        var script = RemoteReadinessAudit.BuildScript(Commit, "T2I_MOSS-Image-Fast");
        Assert.Contains("THUNDER_AUDIT_COMMIT=" + Commit, script);
        Assert.Contains("from manifest import resolve, selected", script);
        Assert.Contains("download_failures.json", script);
        Assert.Contains(".runpod-node-commit", script);
        Assert.Contains(".runpod-node-archive.json", script);
        Assert.Contains("127.0.0.1:8188/object_info", script.Replace("' + path", "object_info"));
        Assert.DoesNotContain("tnr create", script);
        Assert.DoesNotContain("pip install", script);
    }

    [Theory]
    [InlineData("../../bad", "workflow")]
    [InlineData("not-a-commit", "workflow")]
    [InlineData("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "workflow;rm -rf /")]
    public void RejectsBadCommitOrWorkflowSelection(string commit, string workflows)
    {
        Assert.Throws<ArgumentException>(() => RemoteReadinessAudit.BuildScript(commit, workflows));
    }

    [Fact]
    public void OnlyCompletedRemoteAuditCanReturnReady()
    {
        var ok = new CommandResult(0, "[THUNDER-READINESS] status=ready models=80 nodes=14\n", "");
        var failed = new CommandResult(2, "[THUNDER-READINESS] status=blocked reason=missing-pinned-nodes count=1\n", "");
        var invalid = new CommandResult(1, "[THUNDER-READINESS] status=ready models=80 nodes=14\n", "");
        Assert.True(RemoteReadinessAudit.Parse(ok).Ready);
        Assert.Equal(80, RemoteReadinessAudit.Parse(ok).Models);
        Assert.False(RemoteReadinessAudit.Parse(failed).Ready);
        Assert.Contains("missing-pinned-nodes", RemoteReadinessAudit.Parse(failed).Reason);
        Assert.False(RemoteReadinessAudit.Parse(invalid).Ready);
    }

    [Fact]
    public void MissingAuditOutputIsNotSuccess()
    {
        var result = RemoteReadinessAudit.Parse(new CommandResult(0,
            "ComfyUI HTTP 200, Models installed\n", ""));
        Assert.False(result.Ready);
    }
}
