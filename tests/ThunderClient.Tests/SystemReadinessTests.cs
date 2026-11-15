using System.Net;
using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class SystemReadinessTests
{
    private const string FakeGitHubToken = "github_pat_fake_unit_only";
    private const string FakeHfToken = "hf_fake_unit_only";
    private const string FakeCivitaiToken = "civitai_fake_unit_only";

    private sealed class FakeThunderRunner(string json = "[]", int exit = 0) : ICommandRunner
    {
        public int Calls { get; private set; }
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.Equal("tnr", executable);
            Assert.Equal(["status", "--json", "--no-wait"], arguments.ToArray());
            return Task.FromResult(new CommandResult(exit, json, ""));
        }
    }

    private sealed class FixedHttp : HttpMessageHandler
    {
        public readonly List<string> RequestedUrls = [];
        public readonly Dictionary<string, string?> Auth = [];
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.AbsoluteUri;
            RequestedUrls.Add(url);
            Auth[url] = request.Headers.Authorization?.Parameter;
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            if (url == "https://huggingface.co/api/whoami-v2")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            if (url == "https://civitai.com/api/v1/me")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            throw new InvalidOperationException("Unbekannte externe URL");
        }
    }

    private static WorkflowSnapshot Snapshot() =>
        new(new string('a', 40), [], "#!/usr/bin/env bash\n");

    [Fact]
    public async Task LiveStatusIsDifferentFromMerelyStoredKeys()
    {
        var runner = new FakeThunderRunner();
        using var source = new SystemReadinessService(runner, _ => null,
            (_, _) => Task.FromResult(Snapshot()), () => null, new SshService(runner));
        var checks = SystemReadinessService.CreateChecks();
        await source.CheckAllAsync(checks);
        Assert.Equal("Fehlt", checks.Single(x => x.Id == "github").Status);
        Assert.Equal("Erfolgreich getestet", checks.Single(x => x.Id == "thunder").Status);
        Assert.Equal("Nicht prüfbar", checks.Single(x => x.Id == "ssh-key").Status);
        Assert.Equal("Nicht prüfbar", checks.Single(x => x.Id == "ssh-auth").Status);
        Assert.Equal("Optional · fehlt", checks.Single(x => x.Id == "huggingface").Status);
        Assert.Equal("Optional · fehlt", checks.Single(x => x.Id == "civitai").Status);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public async Task AuthenticatedProvidersAreTestedOnlyAgainstOfficialFixedEndpoints()
    {
        var runner = new FakeThunderRunner();
        using var transport = new FixedHttp();
        using var client = new HttpClient(transport);
        string? Secret(string key) => key switch
        {
            "github" => FakeGitHubToken,
            "huggingface" => FakeHfToken,
            "civitai" => FakeCivitaiToken,
            _ => null
        };
        var githubChecked = false;
        using var service = new SystemReadinessService(runner, Secret,
            (token, _) => {
                Assert.Equal(FakeGitHubToken, token);
                githubChecked = true;
                return Task.FromResult(Snapshot());
            }, () => null, new SshService(runner), client);
        var checks = SystemReadinessService.CreateChecks();
        await service.CheckAllAsync(checks);
        Assert.True(githubChecked);
        Assert.Equal("Erfolgreich getestet", checks.Single(x => x.Id == "github").Status);
        Assert.Equal("Erfolgreich getestet", checks.Single(x => x.Id == "huggingface").Status);
        Assert.Equal("Fehler", checks.Single(x => x.Id == "civitai").Status);
        Assert.Equal(FakeHfToken, transport.Auth["https://huggingface.co/api/whoami-v2"]);
        Assert.Equal(FakeCivitaiToken, transport.Auth["https://civitai.com/api/v1/me"]);
        Assert.DoesNotContain(FakeHfToken,
            string.Join(" ", checks.Select(x => x.Detail)));
        Assert.DoesNotContain(FakeCivitaiToken,
            string.Join(" ", checks.Select(x => x.Detail)));
        Assert.Equal(2, transport.RequestedUrls.Count);
    }

    [Fact]
    public async Task InvalidThunderResponseNeverCountsAsAuthenticated()
    {
        var runner = new FakeThunderRunner("{ }");
        using var service = new SystemReadinessService(runner, _ => null,
            (_, _) => Task.FromResult(Snapshot()), () => null, new SshService(runner));
        var checks = SystemReadinessService.CreateChecks();
        await service.CheckAllAsync(checks);
        Assert.Equal("Fehler", checks.Single(x => x.Id == "thunder").Status);
        Assert.Equal("Nicht prüfbar", checks.Single(x => x.Id == "ssh-auth").Status);
    }

    [Fact]
    public async Task GithubApiFailureDoesNotExposeTokenInUserStatus()
    {
        var runner = new FakeThunderRunner();
        using var service = new SystemReadinessService(runner,
            key => key == "github" ? FakeGitHubToken : null,
            (_, _) => throw new InvalidOperationException("token failed"),
            () => null, new SshService(runner));
        var checks = SystemReadinessService.CreateChecks();
        await service.CheckAllAsync(checks);
        var entry = checks.Single(x => x.Id == "github");
        Assert.Equal("Fehler", entry.Status);
        Assert.DoesNotContain(FakeGitHubToken, entry.Detail);
    }

    [Fact]
    public void ReadinessHelpUrlsAreHttpsAndHaveFixedTrustedHosts()
    {
        var hosts = new[] { "github.com", "huggingface.co",
            "learn.microsoft.com", "developer.civitai.com", "civitai.com" };
        var checks = SystemReadinessService.CreateChecks();
        Assert.Equal(7, checks.Count);
        foreach (var item in checks)
        {
            var url = new Uri(item.HelpUrl);
            Assert.Equal("https", url.Scheme);
            Assert.Contains(url.Host, hosts);
            Assert.InRange(item.SetupTabIndex, 0, 7);
            Assert.Equal("Nicht geprüft", item.Status);
        }
    }

    [Fact]
    public void SearchesForOpenSshInPathWithoutRunningNetworkCalls()
    {
        var root = Path.Combine(Path.GetTempPath(), "thunder-open-ssh-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "extra");
        Directory.CreateDirectory(folder);
        var ssh = Path.Combine(folder, "ssh.exe");
        try
        {
            File.WriteAllText(ssh, "test");
            Assert.Equal(ssh, SystemReadinessService.FindExecutable("ssh",
                windowsFolder: Path.Combine(root, "notwindows"), searchPath: folder));
            Assert.Null(SystemReadinessService.FindExecutable("ssh-keyscan",
                windowsFolder: Path.Combine(root, "notwindows"), searchPath: folder));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
