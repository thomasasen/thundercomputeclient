using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed record SelectionEstimate(int TotalFiles, double? TotalGb, int FilesWithoutSize, int Collisions);
public static class WorkflowCatalog
{
    public static IReadOnlyList<WorkflowItem> Load(string checkout)
    {
        var root = Path.Combine(checkout, "workflows");
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("workflows/ im Checkout fehlt.");
        var results = new List<WorkflowItem>();
        foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(directory);
            if (!Regex.IsMatch(name, @"^[a-zA-Z0-9][a-zA-Z0-9_.-]*$")) continue;
            var manifestPath = Path.Combine(directory, "model.json");
            var canvases = Directory.GetFiles(directory, "workflow*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).Where(x => x is not null).Select(x => x!)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            if (!File.Exists(manifestPath) || canvases.Length == 0) continue;

            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(manifestPath));
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
                var readmePath = Path.Combine(directory, "README.md");
                var readme = File.Exists(readmePath) ? File.ReadAllLines(readmePath) : [];
                var title = readme.Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith("# "))
                    ?.TrimStart('#', ' ') ?? name;
                var description = readme.Select(x => x.Trim()).FirstOrDefault(x =>
                    x.Length > 30 && !x.StartsWith('#') && !x.StartsWith('|') &&
                    !x.StartsWith('[') && !x.StartsWith('!')) ?? title;
                results.Add(new WorkflowItem
                {
                    Name = name, Family = title, Description = description,
                    Models = models, CanvasFiles = canvases,
                    ReadmeUrl = $"https://github.com/thomasasen/runpod-ai-workflows/blob/main/workflows/{name}/README.md"
                });
            }
            catch (JsonException) { /* Ungültige Workflows nicht als valide anzeigen */ }
            catch (IOException) { /* Datei beim Update nicht lesbar */ }
        }
        return results;
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

