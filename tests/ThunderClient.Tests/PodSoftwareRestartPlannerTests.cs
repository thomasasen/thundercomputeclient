using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class PodSoftwareRestartPlannerTests
{
    private const string Commit = "b5276af36dd0ccd4477f49de86e4a120a8f8ac54";

    private static WorkflowItem Workflow(string name, string auth = "", params ModelWeight[] models) =>
        new()
        {
            Name = name,
            Description = "Test workflow",
            Family = name,
            ReadmeUrl = "https://example.invalid/",
            CanvasFiles = ["workflow.json"],
            Models = models,
            RequiredAuthorizationEnv = auth
        };

    private static WorkflowSnapshot Snapshot(params WorkflowItem[] items) =>
        new(Commit, items, "#!/usr/bin/env bash\n");

    [Fact]
    public void AcceptsAnExistingWorkflowWithFreshRevision()
    {
        var selected = PodSoftwareRestartPlanner.Validate(
            Snapshot(Workflow("Demo")), ["Demo"]);
        Assert.Single(selected);
        Assert.Equal("Demo", selected[0].Name);
    }

    [Fact]
    public void RefusesMissingOrInvalidWorkflowBeforeStoppingRemoteJob()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PodSoftwareRestartPlanner.Validate(Snapshot(Workflow("Other")), ["Demo"]));
        Assert.Throws<InvalidOperationException>(() =>
            PodSoftwareRestartPlanner.Validate(Snapshot(Workflow("Demo")), []));
        Assert.Throws<InvalidOperationException>(() =>
            PodSoftwareRestartPlanner.Validate(Snapshot(Workflow("Demo")), ["Demo", "Demo"]));
        Assert.Throws<InvalidOperationException>(() =>
            PodSoftwareRestartPlanner.Validate(
                new WorkflowSnapshot("main", [Workflow("Demo")], "#!/usr/bin/env bash\n"), ["Demo"]));
    }

    [Fact]
    public void RefusesGatedAndConflictingModels()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PodSoftwareRestartPlanner.Validate(
                Snapshot(Workflow("Gated", "ACCEPT_LICENSE")), ["Gated"]));

        var a = new ModelWeight("hf", "checkpoints", "shared.safetensors", "source-a", 2, false);
        var b = new ModelWeight("hf", "checkpoints", "shared.safetensors", "source-b", 2, false);
        Assert.Throws<InvalidOperationException>(() =>
            PodSoftwareRestartPlanner.Validate(
                Snapshot(Workflow("A", "", a), Workflow("B", "", b)), ["A", "B"]));
    }

    [Fact]
    public void RejectsShellInjectionWorkflowIdentifiers()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PodSoftwareRestartPlanner.Validate(Snapshot(Workflow("Demo")), ["Demo;true"]));
    }
}
