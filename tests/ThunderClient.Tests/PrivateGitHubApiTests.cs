using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class PrivateGitHubApiTests
{
    private const string CommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TreeSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ManifestSha = "cccccccccccccccccccccccccccccccccccccccc";
    private const string ReadmeSha = "dddddddddddddddddddddddddddddddddddddddd";
    private const string BootstrapSha = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const string CanvasSha = "ffffffffffffffffffffffffffffffffffffffff";
    private const string InvalidSha = "1111111111111111111111111111111111111111";
    private const string PrivateToken = "github_pat_dummy_for_tests_only";
    private const string Manifest = """
        {"models":[{"source":"hf","repo":"owner/model","file":"weights.safetensors",
        "category":"checkpoints","filename":"weights.safetensors","size_gb":1.25}]}
        """;
    private const string Readme = "# T2I Demo\nPrivate text description that has more than thirty characters.\n";
    private const string Bootstrap = "#!/usr/bin/env bash\necho safe\n";

    private sealed class PrivateApiHandler : HttpMessageHandler
    {
        public readonly List<string> Paths = [];
        public bool Unauthorized { get; init; }
        public bool Truncated { get; init; }
        public bool Mismatch { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("api.github.com", request.RequestUri!.Host);
            Assert.Equal(Uri.UriSchemeHttps, request.RequestUri.Scheme);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(PrivateToken, request.Headers.Authorization?.Parameter);
            Assert.DoesNotContain(PrivateToken, request.RequestUri.ToString());
            var path = request.RequestUri.AbsolutePath + request.RequestUri.Query;
            Paths.Add(path);
            if (Unauthorized)
                return Task.FromResult(Reply(new { message = "Bad credentials" }, HttpStatusCode.Unauthorized));

            if (path.EndsWith("/commits/main", StringComparison.Ordinal))
                return Task.FromResult(Reply(new
                {
                    sha = CommitSha,
                    commit = new { tree = new { sha = TreeSha } }
                }));

            if (path.EndsWith("/git/trees/" + TreeSha + "?recursive=1", StringComparison.Ordinal))
                return Task.FromResult(Reply(new
                {
                    truncated = Truncated,
                    tree = new object[]
                    {
                        Node("workflows/T2I_Demo/model.json", ManifestSha, Manifest),
                        Node("workflows/T2I_Demo/README.md", ReadmeSha, Readme),
                        Node("workflows/T2I_Demo/workflow.json", CanvasSha, "{}"),
                        Node("thunder/bootstrap.sh", BootstrapSha, Bootstrap),
                        Node("incoming/Krea 2 + FLUX.2: invalid Windows path/README.md", InvalidSha, "ignore")
                    }
                }));

            if (path.EndsWith("/git/blobs/" + ManifestSha, StringComparison.Ordinal))
                return Task.FromResult(BlobReply(ManifestSha, Manifest, Mismatch));
            if (path.EndsWith("/git/blobs/" + ReadmeSha, StringComparison.Ordinal))
                return Task.FromResult(BlobReply(ReadmeSha, Readme));
            if (path.EndsWith("/git/blobs/" + BootstrapSha, StringComparison.Ordinal))
                return Task.FromResult(BlobReply(BootstrapSha, Bootstrap));
            return Task.FromResult(Reply(new { message = "Unexpected call" }, HttpStatusCode.NotFound));
        }

        private static object Node(string path, string sha, string content) =>
            new { path, sha, type = "blob", size = Encoding.UTF8.GetByteCount(content) };

        private static HttpResponseMessage BlobReply(string sha, string data, bool mismatch = false) =>
            Reply(new { sha = mismatch ? InvalidSha : sha, encoding = "base64",
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(data)) });

        private static HttpResponseMessage Reply(object obj, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code)
            {
                Content = new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json")
            };
    }

    [Fact]
    public async Task ReadsPinnedPrivateWorkflowWithoutCheckingOutInvalidWindowsPath()
    {
        using var handler = new PrivateApiHandler();
        using var client = new HttpClient(handler);
        using var source = new GitHubWorkflowSource(client);
        var snapshot = await source.FetchAsync(PrivateToken);
        Assert.Equal(CommitSha, snapshot.Commit);
        Assert.Single(snapshot.Workflows);
        Assert.Equal("T2I_Demo", snapshot.Workflows[0].Name);
        Assert.Equal(1.25, WorkflowCatalog.Estimate(snapshot.Workflows).TotalGb);
        Assert.Contains("/blob/" + CommitSha + "/", snapshot.Workflows[0].ReadmeUrl);
        Assert.Equal(Bootstrap, snapshot.BootstrapScript);
        Assert.Equal(5, handler.Paths.Count);
        Assert.DoesNotContain(handler.Paths, path => path.Contains("incoming/", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Paths, path => path.Contains("git/checkout", StringComparison.Ordinal));
        Assert.Contains(handler.Paths, path => path.EndsWith("/git/blobs/" + ManifestSha));
    }

    [Fact]
    public async Task RejectsMissingTokenBeforeAnyNetworkCall()
    {
        using var handler = new PrivateApiHandler();
        using var client = new HttpClient(handler);
        using var source = new GitHubWorkflowSource(client);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => source.FetchAsync(""));
        Assert.Contains("Lesetoken", error.Message);
        Assert.Empty(handler.Paths);
    }

    [Fact]
    public async Task InvalidTokenShowsNoPrivateDetails()
    {
        using var handler = new PrivateApiHandler { Unauthorized = true };
        using var client = new HttpClient(handler);
        using var source = new GitHubWorkflowSource(client);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => source.FetchAsync(PrivateToken));
        Assert.Contains("Token", error.Message);
        Assert.DoesNotContain(PrivateToken, error.ToString());
    }

    [Fact]
    public async Task TruncatedGitTreeIsNotSilentlyAccepted()
    {
        using var handler = new PrivateApiHandler { Truncated = true };
        using var client = new HttpClient(handler);
        using var source = new GitHubWorkflowSource(client);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => source.FetchAsync(PrivateToken));
        Assert.Contains("gekürzt", error.Message);
    }

    [Fact]
    public async Task MismatchedGitBlobCannotEnterCatalog()
    {
        using var handler = new PrivateApiHandler { Mismatch = true };
        using var client = new HttpClient(handler);
        using var source = new GitHubWorkflowSource(client);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => source.FetchAsync(PrivateToken));
        Assert.Contains("Blob", error.Message);
    }
}
