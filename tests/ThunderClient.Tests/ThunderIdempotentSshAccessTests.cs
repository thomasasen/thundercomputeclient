using System.Net;
using System.Text;
using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class ThunderIdempotentSshAccessTests
{
    private static readonly string PublicKey =
        "ssh-ed25519 " + Convert.ToBase64String(Enumerable.Repeat((byte)5, 51).ToArray());

    private sealed class FakeKeys : ICommandRunner
    {
        public readonly List<string[]> Calls = [];
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            var argv = arguments.ToArray();
            Calls.Add(argv);
            Assert.Equal("ssh-keygen", executable);
            if (argv.Contains("-t"))
            {
                File.WriteAllText(argv[Array.IndexOf(argv, "-f") + 1],
                    "FAKE PRIVATE KEY (UNIT TEST ONLY)");
                return Task.FromResult(new CommandResult(0, "", ""));
            }
            Assert.Contains("-y", argv);
            return Task.FromResult(new CommandResult(0,
                PublicKey + " user@computer\n", ""));
        }
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        public readonly List<(HttpMethod Method, string Path, string? Bearer, string Body)> Requests = [];
        public readonly Queue<HttpStatusCode> StatusCodes = new();
        public string? ForceResponse;
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" :
                await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.Parameter, body));
            var code = StatusCodes.Count == 0 ? HttpStatusCode.OK : StatusCodes.Dequeue();
            var output = ForceResponse ?? (request.Method == HttpMethod.Get
                ? "{}" : """{"success":true,"uuid":"uuid-pod-01"}""");
            return new HttpResponseMessage(code)
            { Content = new StringContent(output, Encoding.UTF8, "application/json") };
        }
    }

    private static InstanceItem Pod() => new("0", "uuid-pod-01", "Pod",
        "RUNNING", "l40", "203.0.113.1", 40002, null);

    [Fact]
    public async Task PreCreateTokenValidationIsReadOnlyAndNeverEchoesSecrets()
    {
        var net = new FakeHttp();
        var runner = new FakeKeys();
        using var http = new HttpClient(net);
        var access = new ThunderIdempotentSshAccess(http, runner);
        await access.ValidateTokenAsync("secret-api-token");
        Assert.Single(net.Requests);
        Assert.Equal(HttpMethod.Get, net.Requests[0].Method);
        Assert.Equal("/v1/instances/list", net.Requests[0].Path);
        Assert.Equal("secret-api-token", net.Requests[0].Bearer);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task LocalIdentityCreatedOnceAndPublicKeyDerivedFromPrivate()
    {
        var runner = new FakeKeys();
        using var http = new HttpClient(new FakeHttp());
        var access = new ThunderIdempotentSshAccess(http, runner);
        var path = Path.Combine(Path.GetTempPath(), "thunder-keytest-" +
            Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                access.PrepareIdentityAsync(path, CancellationToken.None));
            // A custom key must already exist; never overwrite or auto-create it.
            Assert.Empty(runner.Calls);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task ExistingKeyIsSentOnlyAsPublicKeyNoKeyPairGeneration()
    {
        var runner = new FakeKeys();
        var net = new FakeHttp();
        using var http = new HttpClient(net);
        var access = new ThunderIdempotentSshAccess(http, runner);
        var path = Path.Combine(Path.GetTempPath(),
            "thunder-secret-test-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "PRIVATE UNIT TEST ONLY");
        try
        {
            var identity = await access.PrepareIdentityAsync(path);
            Assert.Equal(PublicKey, identity.PublicKey);
            var ssh = await access.AuthorizeAsync(Pod(), identity,
                "secret-api-token");
            Assert.True(ssh.ProviderConfirmed);
            Assert.Equal("0", ssh.Endpoint.InstanceId);
            Assert.Equal(path, ssh.Endpoint.KeyFile);
            Assert.Single(net.Requests);
            var request = net.Requests[0];
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/instances/0/add_key", request.Path);
            Assert.Equal("secret-api-token", request.Bearer);
            Assert.Contains("\"public_key\"", request.Body);
            Assert.Contains(PublicKey, request.Body);
            Assert.DoesNotContain("PRIVATE UNIT TEST", request.Body);
            Assert.DoesNotContain("\"private_key\"", request.Body);
            Assert.Single(runner.Calls);
            Assert.Contains("-y", runner.Calls[0]);
            Assert.DoesNotContain("-t", runner.Calls[0]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task WrongProviderUuidIsRejectedWithoutPrivateKeyLogging()
    {
        var net = new FakeHttp { ForceResponse =
            """{"success":true,"uuid":"another-pod","key":null}""" };
        using var http = new HttpClient(net);
        var path = Path.GetTempFileName();
        try
        {
            var access = new ThunderIdempotentSshAccess(http, new FakeKeys());
            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                access.AuthorizeAsync(Pod(),
                    new ThunderIdempotentSshAccess.LocalIdentity(path, PublicKey),
                    "secret-api-token"));
            Assert.Contains("andere Instanz-UUID", error.Message);
            Assert.DoesNotContain("secret-api-token", error.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task UnauthorizedTokenBlocksWithoutRepeatedPost()
    {
        var net = new FakeHttp();
        net.StatusCodes.Enqueue(HttpStatusCode.Unauthorized);
        using var http = new HttpClient(net);
        var path = Path.GetTempFileName();
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ThunderIdempotentSshAccess(http, new FakeKeys()).AuthorizeAsync(
                    Pod(), new(path, PublicKey), "secret-api-token"));
            Assert.Contains("HTTP 401", error.Message);
            Assert.Single(net.Requests);
            Assert.DoesNotContain("secret-api-token", error.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ServerFailureRetriesSamePublicKeyExactlyOnce()
    {
        var net = new FakeHttp();
        net.StatusCodes.Enqueue(HttpStatusCode.ServiceUnavailable);
        net.StatusCodes.Enqueue(HttpStatusCode.OK);
        using var http = new HttpClient(net);
        var path = Path.GetTempFileName();
        try
        {
            await new ThunderIdempotentSshAccess(http, new FakeKeys())
                .AuthorizeAsync(Pod(), new(path, PublicKey), "secret-api-token");
            Assert.Equal(2, net.Requests.Count);
            Assert.All(net.Requests, req => {
                Assert.Equal("/v1/instances/0/add_key", req.Path);
                Assert.Contains(PublicKey, req.Body);
            });
            Assert.Equal(net.Requests[0].Body, net.Requests[1].Body);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task AmbiguousServerFailureTriggersReadOnlySshProofRatherThanMoreKeyCreation()
    {
        var net = new FakeHttp();
        net.StatusCodes.Enqueue(HttpStatusCode.ServiceUnavailable);
        net.StatusCodes.Enqueue(HttpStatusCode.ServiceUnavailable);
        using var http = new HttpClient(net);
        var path = Path.GetTempFileName();
        try
        {
            var result = await new ThunderIdempotentSshAccess(http, new FakeKeys())
                .AuthorizeAsync(Pod(), new(path, PublicKey), "secret-api-token");
            Assert.False(result.ProviderConfirmed);
            Assert.Equal(path, result.Endpoint.KeyFile);
            Assert.Equal(2, net.Requests.Count);
            Assert.Equal(net.Requests[0].Body, net.Requests[1].Body);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task RealWindowsOpenSshGeneratesPersistentKeyAndDerivesItsPublicHalf()
    {
        // Genuine local Windows process integration, not a simulated runner.
        // No provider network, no credentials and no remote GPU.
        var folder = Path.Combine(Path.GetTempPath(), "thunder-real-ssh-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "id_ed25519");
        try
        {
            var runner = new ProcessRunner();
            var created = await runner.RunAsync("ssh-keygen",
                ["-q", "-t", "ed25519", "-N", "", "-f", path,
                 "-C", "ThunderClientFixture"], TimeSpan.FromSeconds(20));
            Assert.True(created.Success);
            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path + ".pub"));

            using var http = new HttpClient(new FakeHttp());
            var identity = await new ThunderIdempotentSshAccess(http, runner)
                .PrepareIdentityAsync(path);
            Assert.Equal(path, identity.PrivateFile);
            Assert.StartsWith("ssh-ed25519 ", identity.PublicKey);
            Assert.DoesNotContain("ThunderClientFixture", identity.PublicKey);
            Assert.Equal(2, identity.PublicKey.Split(' ').Length);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void InvalidPublicKeysCannotReachProvider()
    {
        Assert.Throws<InvalidDataException>(() =>
            ThunderIdempotentSshAccess.NormalizePublicKey("ssh-rsa AAAA"));
        Assert.Throws<InvalidDataException>(() =>
            ThunderIdempotentSshAccess.NormalizePublicKey("ssh-ed25519 ${HOME}"));
    }
}
