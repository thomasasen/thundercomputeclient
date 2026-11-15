using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class HardwareRecommendationsTests
{
    private const string RunPodReadme = """
        # T2I_demo
        Some previous table must not be parsed:
        | 2 | historical GPU | 512 GB | Not a recommendation |

        ## Persistent Volume und GPU-Empfehlungen

        **Manifest-Modellbudget (dezimal): 108.780 GB**
        **Persistent Volume (Empfehlung): 133 GB** für den einzelnen Workflow auf /workspace.

        | Rang | RunPod-GPU | VRAM | Begründung |
        | ---: | --- | ---: | --- |
        | 3 | L40S | 48 GB | VRAM-Reserve, keine gemessene Inferenz |
        | 1 | RTX 4090 | 24 GB | Wirtschaftlicher Einstieg, VRAM nicht gemessen |
        | 2 | RTX 5090 | 32 GB | Zusätzliche Reserve |
        """;

    private static WorkflowItem Workflow(string name, double? modelSize,
        string? readme = null, string category = "diffusion_models")
    {
        return new WorkflowItem
        {
            Name = name, Family = name, Description = name,
            ReadmeUrl = "https://github.com/private/blob/commit/README.md",
            CanvasFiles = ["workflow.json"],
            HardwareReference = HardwareRecommendations.Parse(readme),
            Models = [new ModelWeight("hf", category, name + ".safetensors",
                name + "/weights", modelSize, false)]
        };
    }

    [Fact]
    public void ReadsOnlyHardwareSectionAndPreservesOriginalRunPodOrder()
    {
        var reference = HardwareRecommendations.Parse(RunPodReadme);
        Assert.Equal(133, reference.RunPodVolumeGb);
        Assert.Equal(3, reference.Gpus.Count);
        Assert.Equal(["RTX 4090", "RTX 5090", "L40S"],
            reference.Gpus.Select(x => x.Name).ToArray());
        Assert.Equal([24, 32, 48], reference.Gpus.Select(x => x.VramGb).ToArray());
        Assert.DoesNotContain(reference.Gpus, x => x.VramGb == 512);
    }

    [Fact]
    public void NoHardwareMetadataMeansUnknownRatherThanInventingVram()
    {
        var item = Workflow("test", 19.0, "# No GPU reference.");
        var plan = HardwareRecommendations.Plan([item]);
        Assert.Null(plan.ReferenceVramGb);
        Assert.False(plan.CompleteVramReference);
        Assert.Equal(59, plan.SuggestedDiskGb);
        Assert.Contains("keine auswertbare GPU-Tabelle", plan.GpuExplanation);
    }

    [Fact]
    public void SingleWorkflowUsesBothManifestReserveAndRunPodVolumeReference()
    {
        var item = Workflow("T2I_demo", 108.78, RunPodReadme);
        var plan = HardwareRecommendations.Plan([item]);
        Assert.Equal(149, plan.SuggestedDiskGb);
        Assert.Equal(24, plan.ReferenceVramGb);
        Assert.True(plan.CompleteVramReference);
        Assert.Contains("133 GB", plan.StorageExplanation);
        Assert.Contains("keine gemessene Mindestanforderung", plan.GpuExplanation);
        Assert.Contains("Thunder-Disk", plan.StorageExplanation);
    }

    [Fact]
    public void CombinedWorkflowsDeduplicateWeightsAndUseConservativeReferenceClass()
    {
        var anotherReadme = """
            ## Persistent Volume und GPU-Empfehlungen
            **Persistent Volume (Empfehlung): 102 GB**
            | Rang | RunPod-GPU | VRAM | Begründung |
            | ---: | --- | ---: | --- |
            | 1 | H200 | 141 GB | Large model |
            | 5 | H100 PCIe | 80 GB | Lower RunPod reference class |
            """;
        var a = Workflow("T2I_demo", 108.78, RunPodReadme);
        var b = Workflow("I2V_demo", 68.68, anotherReadme);
        var plan = HardwareRecommendations.Plan([a, b]);
        Assert.Equal(218, plan.SuggestedDiskGb);
        Assert.Equal(80, plan.ReferenceVramGb);
        Assert.True(plan.CompleteVramReference);
        Assert.Contains("Kombination mehrerer Workflows", plan.StorageExplanation);
    }

    [Fact]
    public void UnknownManifestSizeDoesNotGenerateDiskRecommendation()
    {
        var plan = HardwareRecommendations.Plan([Workflow("unknown", null, RunPodReadme)]);
        Assert.Null(plan.SuggestedDiskGb);
        Assert.Contains("Nicht alle Modelldateigrößen", plan.StorageExplanation);
    }

    [Fact]
    public void MissingReferenceInOneSelectedWorkflowPreventsCompleteGpuAdvice()
    {
        var plan = HardwareRecommendations.Plan([
            Workflow("one", 6.0, RunPodReadme),
            Workflow("two", 6.0)
        ]);
        Assert.False(plan.CompleteVramReference);
        Assert.Contains("keine auswertbare GPU-Referenz", plan.GpuExplanation);
    }

    [Fact]
    public void EmptySelectionHasNoRecommendation()
    {
        var plan = HardwareRecommendations.Plan([]);
        Assert.Null(plan.SuggestedDiskGb);
        Assert.Null(plan.ReferenceVramGb);
    }

    [Fact]
    public void CatalogParsesHardwareFromActualReadmeText()
    {
        var docs = new[]
        {
            new WorkflowDocument("T2I_demo",
                """{"models":[{"source":"hf","category":"checkpoints","filename":"demo.bin","size_gb":8}]}""",
                RunPodReadme, ["workflow.json"])
        };
        var item = Assert.Single(WorkflowCatalog.LoadFromDocuments(docs,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        Assert.Equal(133, item.HardwareReference.RunPodVolumeGb);
        Assert.Equal("RTX 4090", item.HardwareReference.Gpus[0].Name);
    }
}
