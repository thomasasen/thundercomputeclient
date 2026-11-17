using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public class CatalogTests
{
    [Fact]
    public void DiscoversCanvasesAndDeduplicatesCommonFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "thunder-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "workflows", "T2I_Demo"));
            Directory.CreateDirectory(Path.Combine(root, "workflows", "I2I_Demo"));
            File.WriteAllText(Path.Combine(root, "workflows", "T2I_Demo", "model.json"),
                """{"models":[{"source":"hf","repo":"company/model","file":"weights.bin","category":"checkpoints","filename":"weights.bin","size_gb":3.5,"download":true}]}""");
            File.WriteAllText(Path.Combine(root, "workflows", "I2I_Demo", "model.json"),
                """{"models":[{"source":"hf","repo":"company/model","file":"weights.bin","category":"checkpoints","filename":"weights.bin","size_gb":3.5,"download":true}]}""");
            File.WriteAllText(Path.Combine(root, "workflows", "T2I_Demo", "workflow.json"), "{}");
            File.WriteAllText(Path.Combine(root, "workflows", "T2I_Demo", "workflow.second.json"), "{}");
            File.WriteAllText(Path.Combine(root, "workflows", "I2I_Demo", "workflow.json"), "{}");
            var found = WorkflowCatalog.Load(root);
            Assert.Equal(2, found.Count);
            Assert.Contains(found, x => x.Name == "T2I_Demo" && x.CanvasFiles.Count == 2);
            var estimate = WorkflowCatalog.Estimate(found);
            Assert.Equal(1, estimate.TotalFiles);
            Assert.Equal(3.5, estimate.TotalGb);
            Assert.Equal(0, estimate.Collisions);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ConflictIsNotCountedTwiceButFlagged()
    {
        var a = new WorkflowItem
        {
            Name = "a", Family = "a", Description = "a", ReadmeUrl = "a",
            CanvasFiles = ["workflow.json"],
            Models = [new ModelWeight("hf", "loras", "same.safetensors", "repo-A/file", 1, false)]
        };
        var b = new WorkflowItem
        {
            Name = "b", Family = "b", Description = "b", ReadmeUrl = "b",
            CanvasFiles = ["workflow.json"],
            Models = [new ModelWeight("hf", "loras", "same.safetensors", "repo-B/file", 2, false)]
        };
        var summary = WorkflowCatalog.Estimate([a, b]);
        Assert.Equal(1, summary.TotalFiles);
        Assert.Equal(1, summary.Collisions);
    }

    [Fact]
    public void DistinctHuggingFaceRevisionsAndSnapshotFilesCannotAlias()
    {
        static WorkflowItem Parse(string name, string manifest)
        {
            var result = WorkflowCatalog.LoadFromDocuments(
                [new WorkflowDocument(name, manifest, "# Test\n", ["workflow.json"])],
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            return Assert.Single(result);
        }

        var a = Parse("T2I_RevA", """
            {"models":[{"source":"hf","repo":"org/model","file":"weights.safetensors",
            "revision":"first","category":"checkpoints","filename":"shared.safetensors","size_gb":2}]}
            """);
        var b = Parse("T2I_RevB", """
            {"models":[{"source":"hf","repo":"org/model","file":"weights.safetensors",
            "revision":"second","category":"checkpoints","filename":"shared.safetensors","size_gb":2}]}
            """);
        Assert.Equal(1, WorkflowCatalog.Estimate([a, b]).Collisions);
        var c = Parse("T2I_SnapA", """
            {"models":[{"source":"hf_snapshot","repo":"org/snapshot",
            "revision":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","files":["model.safetensors","config.json"],
            "category":"checkpoints","filename":"snapshot","size_gb":3}]}
            """);
        var d = Parse("T2I_SnapB", """
            {"models":[{"source":"hf_snapshot","repo":"org/snapshot",
            "revision":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","files":["model.safetensors","config.json"],
            "category":"checkpoints","filename":"snapshot","size_gb":3}]}
            """);
        Assert.Equal(1, WorkflowCatalog.Estimate([c, d]).Collisions);
    }

    [Fact]
    public void UnknownSizeIsNotInvented()
    {
        var workflow = new WorkflowItem
        {
            Name = "x", Family = "x", Description = "x", ReadmeUrl = "x", CanvasFiles = [],
            Models = [new ModelWeight("hf", "checkpoints", "model.bin", "repo/model", null, false)]
        };
        var summary = WorkflowCatalog.Estimate([workflow]);
        Assert.Null(summary.TotalGb);
        Assert.Equal(1, summary.FilesWithoutSize);
    }
}

