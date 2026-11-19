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
        var quoted = line.Split('\'');
        Assert.True(quoted.Length >= 5);
        var worker = Encoding.UTF8.GetString(Convert.FromBase64String(quoted[3]));
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
    public void ExplicitRestartOnlyTargetsTrackedDetachedSessionWithoutRemovingModels()
    {
        var restart = RemoteBootstrapSupervisor.BuildLaunchScript(
            Adapter, Commit, "T2I_Demo", "github-secret", "", "", restart: true);
        var normal = RemoteBootstrapSupervisor.BuildLaunchScript(
            Adapter, Commit, "T2I_Demo", "github-secret", "", "");

        Assert.Contains("'1' == '1'", restart);
        Assert.Contains("'0' == '1'", normal);
        Assert.Contains("THUNDER_CLIENT_RUN_ID=$active_run", restart);
        Assert.Contains(@"$pgid"" != ""$active""", restart);
        Assert.Contains(@"$sid"" != ""$active""", restart);
        Assert.Contains("kill -TERM -- \"-$active\"", restart);
        Assert.Contains("kill -0 -- \"-$active\"", restart);
        Assert.Contains("Vorherige Prozessgruppe beendet; Modelle und Workspace bleiben erhalten", restart);
        Assert.Contains("Prepare the new, credential-free worker BEFORE stopping", restart);
        Assert.DoesNotContain("rm -rf", restart);
        Assert.DoesNotContain("tnr delete", restart);
        Assert.DoesNotContain("reboot", restart);
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
