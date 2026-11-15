using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed record SelectionEstimate(int TotalFiles, double? TotalGb, int FilesWithoutSize, int Collisions);
public sealed record WorkflowDocument(string Name, string ManifestJson, string? Readme,
    IReadOnlyList<string> CanvasFiles);

public static class WorkflowCatalog
{
    // In-memory parsing is shared by the authenticated GitHub API and local fixture tests.
    // No Git checkout or path-dependent IO is required for the real workflow catalog.
    public static IReadOnlyList<WorkflowItem> LoadFromDocuments(
        IEnumerable<WorkflowDocument> documents, string commit)
    {
        var results = new List<WorkflowItem>();
        foreach (var document in documents.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!Regex.IsMatch(document.Name, @"^[a-zA-Z0-9][a-zA-Z0-9_.-]*$"))
                continue;
            var canvases = document.CanvasFiles
                .Where(x => Regex.IsMatch(x, @"^workflow[^/\\]*\.json$", RegexOptions.IgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (canvases.Length == 0) continue;
            try
            {
                using var json = JsonDocument.Parse(document.ManifestJson);
                var data = json.RootElement;
                if (!data.TryGetProperty("models", out var modelArray) ||
                    modelArray.ValueKind != JsonValueKind.Array) continue;
                var models = new List<ModelWeight>();
                foreach (var model in modelArray.EnumerateArray())
                {
                    if (model.ValueKind != JsonValueKind.Object) continue;
                    var category = GetString(model, "category");
                    var filename = GetString(model, "filename");
                    if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(filename)) continue;
                    if (model.TryGetProperty("download", out var download) &&
                        download.ValueKind == JsonValueKind.False) continue;
                    var source = GetString(model, "source");
                    var sourceId = source == "hf"
                        ? GetString(model, "repo") + "/" + GetString(model, "file")
                        : source + ":" + GetString(model, "version_id") + ":" + GetString(model, "sha256");
                    double? size = null;
                    if (model.TryGetProperty("size_gb", out var sizeValue) &&
                        sizeValue.ValueKind == JsonValueKind.Number &&
                        sizeValue.TryGetDouble(out var gb) && gb >= 0) size = gb;
                    var estimated = model.TryGetProperty("size_estimated", out var es) &&
                        es.ValueKind == JsonValueKind.True;
                    models.Add(new ModelWeight(source, category, filename, sourceId, size, estimated));
                }
                var lines = (document.Readme ?? "").Split('\n');
                var title = lines.Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith("# "))
                    ?.TrimStart('#', ' ') ?? document.Name;
                var description = lines.Select(x => x.Trim()).FirstOrDefault(x =>
                    x.Length > 30 && !x.StartsWith('#') && !x.StartsWith('|') &&
                    !x.StartsWith('[') && !x.StartsWith('!')) ?? title;
                results.Add(new WorkflowItem
                {
                    Name = document.Name, Family = title, Description = description,
                    Models = models, CanvasFiles = canvases,
                    HardwareReference = HardwareRecommendations.Parse(document.Readme),
                    ReadmeUrl = $"https://github.com/thomasasen/runpod-ai-workflows/blob/{commit}/workflows/{document.Name}/README.md"
                });
            }
            catch (JsonException)
            {
                // An invalid manifest is never interpreted as a valid workflow.
            }
        }
        return results;
    }

    // Retained for local fixture/tests only; the desktop app uses LoadFromDocuments.
    public static IReadOnlyList<WorkflowItem> Load(string checkout)
    {
        var root = Path.Combine(checkout, "workflows");
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("workflows/ fehlt.");
        var docs = new List<WorkflowDocument>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var manifest = Path.Combine(directory, "model.json");
            if (!File.Exists(manifest)) continue;
            var canvases = Directory.GetFiles(directory, "workflow*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).Where(x => x is not null).Select(x => x!).ToArray();
            if (canvases.Length == 0) continue;
            var readme = Path.Combine(directory, "README.md");
            docs.Add(new WorkflowDocument(Path.GetFileName(directory),
                File.ReadAllText(manifest), File.Exists(readme) ? File.ReadAllText(readme) : null,
                canvases));
        }
        return LoadFromDocuments(docs, "main");
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => ""
        };
    }

    public static SelectionEstimate Estimate(IEnumerable<WorkflowItem> selected)
    {
        var deduplicated = new Dictionary<string, ModelWeight>(StringComparer.OrdinalIgnoreCase);
        var collisions = 0;
        foreach (var weight in selected.SelectMany(x => x.Models))
        {
            if (deduplicated.TryGetValue(weight.TargetKey, out var existing))
            {
                if (!string.Equals(existing.SourceId, weight.SourceId, StringComparison.OrdinalIgnoreCase))
                    collisions++;
                continue;
            }
            deduplicated[weight.TargetKey] = weight;
        }
        var values = deduplicated.Values.ToArray();
        var missing = values.Count(x => !x.SizeGb.HasValue);
        return new SelectionEstimate(values.Length,
            missing > 0 ? null : values.Sum(x => x.SizeGb!.Value), missing, collisions);
    }
}

