using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed record WorkflowSnapshot(string Commit, IReadOnlyList<WorkflowItem> Workflows,
    string BootstrapScript);

/// <summary>
/// Read-only access to exactly one private GitHub repository over the authenticated
/// HTTPS REST API. Never clones or checks out the repository, including invalid
/// Windows paths under incoming/. Blob IDs and tree are pinned to one main commit.
/// </summary>
public sealed class GitHubWorkflowSource : IDisposable
{
    private const string RepositoryApi = "https://api.github.com/repos/thomasasen/runpod-ai-workflows/";
    private const int MaxJsonBytes = 5_000_000;
    private const int MaxWorkflows = 200;
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    private sealed record Blob(string Sha, long Size);
    private sealed record Candidate(string Name, Blob Manifest, Blob? Readme,
        IReadOnlyList<string> Canvases);

    public GitHubWorkflowSource(HttpClient? http = null)
    {
        _ownsClient = http is null;
        _http = http ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        }) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<WorkflowSnapshot> FetchAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(
                "Für das private GitHub-Workflow-Repository fehlt ein Lesetoken. " +
                "Unter Zugangsdaten einen Fine-grained PAT für runpod-ai-workflows " +
                "mit Contents: Read-only hinterlegen und anschließend Katalog aktualisieren.");

        using var commitInfo = await GetJsonAsync("commits/main", token, ct);
        var commit = CheckedSha(commitInfo.RootElement.GetProperty("sha").GetString());
        var treeSha = CheckedSha(commitInfo.RootElement.GetProperty("commit")
            .GetProperty("tree").GetProperty("sha").GetString());

        using var treeJson = await GetJsonAsync("git/trees/" + treeSha + "?recursive=1", token, ct);
        if (treeJson.RootElement.GetProperty("truncated").GetBoolean())
            throw new InvalidDataException("GitHub hat den Repository-Baum gekürzt. Der Katalog wird nicht unvollständig übernommen.");

        var files = new Dictionary<string, Blob>(StringComparer.Ordinal);
        foreach (var node in treeJson.RootElement.GetProperty("tree").EnumerateArray())
        {
            if (node.GetProperty("type").GetString() != "blob") continue;
            var path = node.GetProperty("path").GetString() ?? "";
            // Never read incoming/, .git or unknown locations. Invalid Windows
            // characters elsewhere in the repository remain harmless API metadata.
            if (!(path.StartsWith("workflows/", StringComparison.Ordinal) ||
                  path == "thunder/bootstrap.sh")) continue;
            var sha = CheckedSha(node.GetProperty("sha").GetString());
            var size = node.TryGetProperty("size", out var val) && val.TryGetInt64(out var n) ? n : -1;
            files[path] = new Blob(sha, size);
        }

        if (!files.TryGetValue("thunder/bootstrap.sh", out var adapter))
            throw new InvalidDataException("thunder/bootstrap.sh fehlt im privaten Repository.");
        var bootstrap = await ReadBlobAsync(adapter, 250_000, token, ct);
        if (!bootstrap.StartsWith("#!/usr/bin/env bash\n", StringComparison.Ordinal))
            throw new InvalidDataException("Unerwarteter Thunder-Bootstrap-Header.");

        var byWorkflow = files.Where(x => x.Key.StartsWith("workflows/", StringComparison.Ordinal))
            .Select(x => (entry: x, parts: x.Key.Split('/')))
            .Where(x => x.parts.Length == 3 &&
                Regex.IsMatch(x.parts[1], @"^[a-zA-Z0-9][a-zA-Z0-9_.-]*$"))
            .GroupBy(x => x.parts[1], StringComparer.Ordinal)
            .ToArray();

        var candidates = new List<Candidate>();
        foreach (var group in byWorkflow)
        {
            var entries = group.ToDictionary(x => x.parts[2], x => x.entry.Value,
                StringComparer.Ordinal);
            if (!entries.TryGetValue("model.json", out var manifest)) continue;
            var canvases = entries.Keys
                .Where(x => Regex.IsMatch(x, @"^workflow[^/\\]*\.json$", RegexOptions.IgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (canvases.Length == 0) continue;
            entries.TryGetValue("README.md", out var readme);
            candidates.Add(new Candidate(group.Key, manifest, readme, canvases));
        }
        if (candidates.Count == 0 || candidates.Count > MaxWorkflows)
            throw new InvalidDataException("Unerwartete Anzahl von Workflows im privaten Repository.");

        using var throttle = new SemaphoreSlim(6);
        var fetches = candidates.Select(async candidate =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                var manifest = await ReadBlobAsync(candidate.Manifest, 700_000, token, ct);
                var readme = candidate.Readme is null ? null :
                    await ReadBlobAsync(candidate.Readme, 120_000, token, ct);
                return new WorkflowDocument(candidate.Name, manifest, readme,
                    candidate.Canvases);
            }
            finally { throttle.Release(); }
        }).ToArray();
        var documents = await Task.WhenAll(fetches);
        var workflows = WorkflowCatalog.LoadFromDocuments(documents, commit);
        if (workflows.Count == 0)
            throw new InvalidDataException("Keine gültigen Workflows in der privaten Repository-Revision.");
        return new WorkflowSnapshot(commit, workflows, bootstrap);
    }

    private async Task<string> ReadBlobAsync(Blob blob, int limit, string token, CancellationToken ct)
    {
        if (blob.Size < 0 || blob.Size > limit)
            throw new InvalidDataException("Eine benötigte GitHub-Datei überschreitet das sichere Größenlimit.");
        using var data = await GetJsonAsync("git/blobs/" + blob.Sha, token, ct);
        var root = data.RootElement;
        if (root.GetProperty("encoding").GetString() != "base64" ||
            CheckedSha(root.GetProperty("sha").GetString()) != blob.Sha)
            throw new InvalidDataException("GitHub hat keine erwartete, commit-fixierte Blob-Datei geliefert.");
        var base64 = root.GetProperty("content").GetString() ?? "";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(base64); }
        catch (FormatException ex) { throw new InvalidDataException("Ungültiger GitHub-Blob-Inhalt.", ex); }
        if (bytes.Length > limit || bytes.Length != blob.Size)
            throw new InvalidDataException("GitHub-Dateigröße stimmt nicht mit der Revision überein.");
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException ex) { throw new InvalidDataException("Ungültige UTF-8-Datei.", ex); }
    }

    private async Task<JsonDocument> GetJsonAsync(string relative, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RepositoryApi + relative);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("ThunderComputeClient/0.1");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "GitHub-Token ungültig oder abgelaufen.",
                HttpStatusCode.Forbidden => "Zugriff verweigert oder GitHub-API-Rate-Limit erreicht. Tokenrechte prüfen.",
                HttpStatusCode.NotFound => "Privates GitHub-Repository nicht gefunden oder Token hat keinen Leserechtezugriff.",
                _ => "GitHub-API meldet HTTP " + (int)response.StatusCode
            };
            throw new InvalidOperationException(detail);
        }
        // No unbounded responses and no redirects to arbitrary servers with credentials.
        await response.Content.LoadIntoBufferAsync(MaxJsonBytes, ct);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static string CheckedSha(string? value)
    {
        if (value is null || !Regex.IsMatch(value, @"\A[a-fA-F0-9]{40}\z"))
            throw new InvalidDataException("Ungültige Commit-/Objekt-ID von GitHub.");
        return value.ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
