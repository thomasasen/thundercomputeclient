using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class BootstrapPinningTests
{
    private const string Sha = "b5276af36dd0ccd4477f49de86e4a120a8f8ac54";
    private const string MinimalAdapter = """
#!/usr/bin/env bash
if [[ "${REPO_REF:-main}" != 'main' ]]; then
    exit 1
fi
export REPO_REF=main
url = 'https://api.github.com/repos/thomasasen/runpod-ai-workflows/contents/scripts/pod_entry.py?ref=main'
""";

    [Fact]
    public void UsesExactlyReviewedCommitForEntrypointAndWorker()
    {
        var result = SshService.PinBootstrapAdapter(MinimalAdapter, Sha);
        Assert.Contains("export REPO_REF=" + Sha, result);
        Assert.Contains("contents/scripts/pod_entry.py?ref=" + Sha, result);
        Assert.Contains("if [[ \"${REPO_REF:-" + Sha + "}\" != '" + Sha + "' ]]; then", result);
        Assert.DoesNotContain("contents/scripts/pod_entry.py?ref=main", result);
    }

    [Fact]
    public void AdapterVersionChangeCannotQuietlyFallBackToMain()
    {
        Assert.Throws<InvalidDataException>(() =>
            SshService.PinBootstrapAdapter(MinimalAdapter.Replace("export REPO_REF=main",
                "export REPO_REF=latest"), Sha));
        Assert.Throws<InvalidDataException>(() =>
            SshService.PinBootstrapAdapter(MinimalAdapter, "main"));
    }
}
