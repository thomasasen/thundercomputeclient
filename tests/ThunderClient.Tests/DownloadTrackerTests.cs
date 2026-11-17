using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public class DownloadTrackerTests
{
    [Fact]
    public void ParsesQuotedModelNameAndRealProgress()
    {
        var line = """[DOWNLOAD-STATUS] FORTSCHRITT "hf:checkpoints/weight.safetensors": 2.00 GB / 5.00 GB (40%)""";
        Assert.True(DownloadTracker.TryParse(line, out var item));
        Assert.Equal("hf:checkpoints/weight.safetensors", item!.Model);
        Assert.Equal("FORTSCHRITT", item.State);
        Assert.Contains("40%", item.Progress);
    }

    [Fact]
    public void ReadsCompletionButDoesNotInventPercent()
    {
        Assert.True(DownloadTracker.TryParse(
            """[DOWNLOAD-STATUS] GELADEN "civitai:loras/demo.safetensors" """, out var item));
        Assert.Equal("GELADEN", item!.State);
        Assert.DoesNotContain("%", item.Progress);
    }

    [Fact]
    public void RejectsMalformedAndUnrelatedLogLines()
    {
        Assert.False(DownloadTracker.TryParse("[START-TIMING] phase=prepare elapsed_s=3", out _));
        Assert.False(DownloadTracker.TryParse("[DOWNLOAD-STATUS] FORTSCHRITT unquoted", out _));
    }
}

