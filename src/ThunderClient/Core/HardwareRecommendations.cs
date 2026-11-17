using System.Text;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed record ReferenceGpu(int Rank, string Name, int VramGb, string Reason);

/// <summary>
/// Source: the private workflow README's RunPod section. No Thunder
/// hardware or availability assertions are implied by these values.
/// </summary>
public sealed record WorkflowHardwareReference(int? RunPodVolumeGb, IReadOnlyList<ReferenceGpu> Gpus)
{
    public static WorkflowHardwareReference Empty { get; } = new(null, []);
}

public sealed record HardwarePlan(
    int? SuggestedDiskGb, int? ReferenceVramGb,
    string StorageExplanation, string GpuExplanation, bool CompleteVramReference);

public static class HardwareRecommendations
{
    private static readonly Regex Volume = new(
        @"Persistent Volume\s*\(Empfehlung\)\s*:\s*(?<gb>\d{1,4})\s*GB",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex RankedGpu = new(
        @"^\|\s*(?<rank>[1-9]\d?)\s*\|\s*(?<name>[^|]{1,80})\s*\|\s*(?<vram>\d{1,3})\s*GB\s*\|\s*(?<reason>[^|]{1,400})\|",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public static WorkflowHardwareReference Parse(string? readme)
    {
        if (string.IsNullOrWhiteSpace(readme)) return WorkflowHardwareReference.Empty;
        // Exclude other markdown tables, including model catalogs and source links.
        var section = readme.IndexOf("## Persistent Volume und GPU-Empfehlungen",
            StringComparison.OrdinalIgnoreCase);
        if (section < 0) return WorkflowHardwareReference.Empty;
        var tail = readme[section..].Replace("\r\n", "\n");
        var next = tail.IndexOf("\n## ", 3, StringComparison.Ordinal);
        if (next >= 0) tail = tail[..next];
        int? volumeGb = null;
        var match = Volume.Match(tail);
        if (match.Success && int.TryParse(match.Groups["gb"].Value, out var gb) &&
            gb is >= 1 and <= 2000) volumeGb = gb;
        var gpus = new List<ReferenceGpu>();
        foreach (Match row in RankedGpu.Matches(tail))
        {
            if (!int.TryParse(row.Groups["rank"].Value, out var rank) ||
                !int.TryParse(row.Groups["vram"].Value, out var vram) ||
                vram is < 8 or > 400) continue;
            gpus.Add(new ReferenceGpu(rank,
                row.Groups["name"].Value.Trim(),
                vram, row.Groups["reason"].Value.Trim()));
        }
        return new WorkflowHardwareReference(volumeGb,
            gpus.OrderBy(x => x.Rank).ToArray());
    }

    public static HardwarePlan Plan(IReadOnlyList<WorkflowItem> selected)
    {
        if (selected.Count == 0)
            return new HardwarePlan(null, null,
                "Zuerst mindestens einen Workflow auswählen.",
                "GPU-Empfehlungen erscheinen nach der Workflow-Auswahl.", false);
        var estimate = WorkflowCatalog.Estimate(selected);
        // The upstream volume refers to RunPod's /workspace, not the complete
        // Thunder instance disk. Never equate it with a verified Thunder minimum.
        var runpodVolumes = selected
            .Where(x => x.HardwareReference.RunPodVolumeGb.HasValue)
            .Select(x => x.HardwareReference.RunPodVolumeGb!.Value).ToArray();
        var minimumFromManifest = estimate.TotalGb.HasValue
            ? (int?)Math.Ceiling(estimate.TotalGb.Value + 40.0)
            : null;
        var plannedDisk = new[] {
            minimumFromManifest ?? 0,
            runpodVolumes.Length == 0 ? 0 : runpodVolumes.Max(),
            50
        }.Max();
        var suggestedDisk = estimate.TotalGb.HasValue ? (int?)plannedDisk : null;

        var sources = selected
            .Where(x => x.HardwareReference.Gpus.Count > 0).ToArray();
        var complete = sources.Length == selected.Count;
        var vram = sources.Length == 0 ? (int?)null :
            sources.Max(x => x.HardwareReference.Gpus.Min(g => g.VramGb));
        var storage = new StringBuilder();
        storage.Append(estimate.TotalGb.HasValue
            ? $"Modelldateien (dedupliziert): {estimate.TotalGb.Value:N2} GB; " +
              $"Planungsansatz für gesamte Thunder-Disk: {suggestedDisk} GB " +
              "(Modelldateien + 40 GB pauschale Reserve)."
            : "Nicht alle Modelldateigrößen sind bekannt. Thunder-Disk nicht automatisch empfehlen.");
        if (runpodVolumes.Length > 0)
            storage.Append(" README-Referenz: RunPod-Persistent-Volume " +
                string.Join(", ", selected.Where(x => x.HardwareReference.RunPodVolumeGb.HasValue)
                    .Select(x => x.Name + ": " + x.HardwareReference.RunPodVolumeGb + " GB")) +
                ". Das ist keine gemessene Thunder-Diskanforderung.");
        if (selected.Count > 1)
            storage.Append(" Kombination mehrerer Workflows ist nicht separat auf Laufzeit-/Cachebedarf vermessen.");
        if (estimate.Collisions > 0)
            storage.Append($" ACHTUNG: {estimate.Collisions} Modelldatei-Konflikte.");

        var gpu = new StringBuilder();
        if (vram.HasValue)
            gpu.Append($"VRAM-Orientierung: {vram} GB oder mehr " +
                "(kleinste VRAM-Klasse unter den jeweils im README gelisteten RunPod-GPUs; " +
                "keine gemessene Mindestanforderung für Thunder).");
        else gpu.Append("Im README liegt keine auswertbare GPU-Tabelle vor; GPU-Auswahl manuell prüfen.");

        foreach (var item in selected.Take(4))
        {
            if (item.HardwareReference.Gpus.Count > 0)
                gpu.Append("\n" + item.Name + ": " +
                    string.Join(" · ", item.HardwareReference.Gpus.Take(3)
                        .Select(x => x.Rank + ". " + x.Name + " (" + x.VramGb + " GB)")));
            else gpu.Append("\n" + item.Name + ": keine dokumentierte GPU-Empfehlung.");
        }
        if (!complete) gpu.Append("\nMindestens ein Workflow hat keine auswertbare GPU-Referenz.");
        gpu.Append("\nGPU-Modelle, Treiber, VRAM-Offloading, Preise und Verfügbarkeit bei Thunder separat prüfen.");

        return new HardwarePlan(suggestedDisk, vram, storage.ToString(), gpu.ToString(), complete);
    }
}
