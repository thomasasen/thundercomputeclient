using System.Text;
using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class RemoteBootstrapSupervisorTests
{
    private const string Commit = "b5276af36dd0ccd4477f49de86e4a120a8f8ac54";
    private const string Adapter = "#!/usr/bin/env bash\n" +
        "if [[ \"${REPO_REF:-main}\" != 'main' ]]; then\n" +
        "    exit 1\nfi\n" +
        "export REPO_REF=main\n" +
        "url = 'https://api.github.com/repos/thomasasen/runpod-ai-workflows/contents/scripts/pod_entry.py?ref=main'\n";

    [Fact]
    public void StartsOneDetachedRunWithoutPersistingCredentialsInWorker()
    {
        var result = RemoteBootstrapSupervisor.BuildLaunchScript(
            Adapter, Commit, "T2I_Demo", "github-secret-SENT-ONLY-OVER-SSH",
            "hf-secret", "civitai-secret");
        Assert.Contains("nohup setsid bash", result);
        Assert.Contains("THUNDER_CLIENT_RUN_ID=", result);
        Assert.Contains("flock -w 15", result);
        Assert.Contains("Bestehenden Remote-Job wieder aufnehmen", result);
        Assert.Contains("base64 --decode", result);
        var line = result.Split('\n').Single(x => x.Contains("base64 --decode"));
        var first = line.IndexOf('\'');
        var last = line.IndexOf('\'', first + 1);
        var worker = Encoding.UTF8.GetString(Convert.FromBase64String(line[(first + 1)..last]));
        Assert.Contains("REPO_REF=" + Commit, worker);
        Assert.DoesNotContain("github-secret", worker);
        Assert.DoesNotContain("hf-secret", worker);
        Assert.DoesNotContain("civitai-secret", worker);
        Assert.DoesNotContain("?ref=main", worker);
    }

    [Fact]
    public void RejectsShellInjectionInWorkflowSelection()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RemoteBootstrapSupervisor.BuildLaunchScript(Adapter, Commit,
                "Demo;cat /etc/passwd", "token", "", ""));
    }

    [Fact]
    public void ReattachIsBoundToPidAndRunIdentity()
    {
        var script = RemoteBootstrapSupervisor.BuildFollowScript();
        Assert.Contains("/proc/$pid/environ", script);
        Assert.Contains("THUNDER_CLIENT_RUN_ID=$run", script);
        Assert.Contains("tail -n +1 -f --pid", script);
        Assert.DoesNotContain("GITHUB_TOKEN", script);
    }
}
