using System.Collections.ObjectModel;
using System.Globalization;

namespace ThunderClient.Core;

public sealed record ModelWeight(
    string Source, string Category, string Filename, string SourceId, double? SizeGb, bool Estimated)
{
    public string TargetKey => (Category + "/" + Filename).ToLowerInvariant();
    public string Display => Category + "/" + Filename;
}

public sealed class WorkflowItem : Bindable
{
    private bool _selected;
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Family { get; init; }
    public required string ReadmeUrl { get; init; }
    public required IReadOnlyList<string> CanvasFiles { get; init; }
    public required IReadOnlyList<ModelWeight> Models { get; init; }
    public WorkflowHardwareReference HardwareReference { get; init; } = WorkflowHardwareReference.Empty;
    public string RequiredAuthorizationEnv { get; init; } = "";
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
    public string Canvases => string.Join(", ", CanvasFiles);
    public string ModelCount => Models.Count.ToString(CultureInfo.InvariantCulture);
    public string ModelSize => Models.All(x => x.SizeGb.HasValue)
        ? $"{Models.Sum(x => x.SizeGb!.Value):N2} GB" : "unbekannt / unvollständig";
    public string Weights => string.Join(", ", Models.Take(12).Select(x => x.Filename)) +
        (Models.Count > 12 ? $" … (+{Models.Count - 12})" : "");
}

/// <summary>
/// Thunder exposes A100 80GB as "a100xl" in its v2/specs, v2/pricing
/// and create API. The app keeps the pre-existing local "a100" key
/// for persisted UI ordering; only provider boundaries use this canonical key.
/// Source: Thunder-Compute/thunder-cli utils/spec_store.go.
/// </summary>
public static class ThunderGpuNames
{
    public static string ToProviderKey(string key) => key?.Trim().ToLowerInvariant() switch
    {
        "a100" or "a100xl" => "a100xl",
        "a6000" => "a6000",
        "l40" => "l40",
        "h100" => "h100",
        _ => throw new InvalidOperationException(
            "Unbekannte Thunder-GPU-Kennung. Anbieterprüfung und kostenpflichtige Erstellung gesperrt.")
    };
}

public sealed class GpuChoice : Bindable
{
    private bool _enabled = true;
    private int _vcpus;
    private decimal _gpuHourlyEstimate;
    public GpuChoice(string key, string label, int vramGb, int vcpus, decimal estimate)
    { Key = key; Label = label; VramGb = vramGb; _vcpus = vcpus; _gpuHourlyEstimate = estimate; }
    public string Key { get; }
    public string Label { get; }
    public int VramGb { get; }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public int Vcpus { get => _vcpus; set => Set(ref _vcpus, value); }
    public decimal GpuHourlyEstimate { get => _gpuHourlyEstimate; set => Set(ref _gpuHourlyEstimate, value); }
    public string Display => $"{Label} ({VramGb} GB VRAM)";
}

public sealed record InstanceItem(string Id, string Uuid, string Name, string Status,
    string Gpu, string Ip, int Port, DateTimeOffset? Created, int StorageGb = 0, int GpuCount = 0)
{
    public string Display => $"{Id} · {Name} · {Gpu} · {Status}";
    public string SshPortDisplay => Port is >= 1 and <= 65535
        ? Port.ToString(CultureInfo.InvariantCulture) : "Noch nicht bereit";
}

public sealed record SshConnection(string InstanceId, string Host, int Port, string KeyFile);

public sealed class ClientSettings
{
    public int DiskGb { get; set; } = 160;
    public int GpuCount { get; set; } = 1;
    public decimal MaximumHourlyUsd { get; set; } = 1.35m;
    public string Template { get; set; } = "base";
    public string Theme { get; set; } = "Hell";
    public bool OfflineApproved { get; set; }
    public string LocalGitRef { get; set; } = "main";
    public List<string> SelectedWorkflows { get; set; } = [];
    public List<string> GpuOrder { get; set; } = ["a6000", "l40", "a100", "h100"];
}

public static class Estimates
{
    // Historical published list estimates from 2026-11-15, NOT live provider quotations.
    // Published Thunder instance list rates (checked 2026-11-16), NOT live quotes.
    // Fail closed for unknown GPU families rather than approving an invented tariff.
    public const decimal AdditionalDiskGbMonthUsd = 0.219m;
    public const decimal AdditionalVcpuHourlyUsd = 0.04m;

    public static decimal Hourly(GpuChoice gpu, int gpuCount, int diskGb)
    {
        if (gpuCount < 1 || gpu.Vcpus < 1 || diskGb < 1)
            throw new ArgumentOutOfRangeException(nameof(gpuCount), "GPU, vCPU und Disk müssen positiv sein.");
        var (includedCpusPerGpu, publishedGpuMinimum) = gpu.Key.ToLowerInvariant() switch
        {
            "a6000" => (6, .35m),
            "l40" => (6, .79m),
            "a100" or "a100xl" => (4, 1.09m),
            "h100" => (4, 3.20m),
            _ => throw new InvalidOperationException(
                "Kein veröffentlichter Thunder-Tarif für diese GPU bekannt. Kostenfreigabe gesperrt.")
        };
        // Edited GUI price estimates must not lower the known published list floor.
        var gpuHourly = Math.Max(gpu.GpuHourlyEstimate, publishedGpuMinimum) * gpuCount;
        var additionalCpus = Math.Max(0, gpu.Vcpus - includedCpusPerGpu * gpuCount);
        var cpuHourly = additionalCpus * AdditionalVcpuHourlyUsd;
        var diskHourly = Math.Max(0, diskGb - 100 * gpuCount) *
                         AdditionalDiskGbMonthUsd / 730m;
        return gpuHourly + cpuHourly + diskHourly;
    }
    public static string CapacityWarning(double? manifestGb, int diskGb)
    {
        if (!manifestGb.HasValue) return "Manifestgröße nicht vollständig bekannt. Disk manuell prüfen.";
        if (diskGb < manifestGb.Value + 40)
            return $"WARNUNG: {diskGb} GB Gesamtdisk bei {manifestGb:N2} GB Modellen. Betriebssystem, venvs, Cache und Outputs benötigen zusätzlich Platz (40 GB nur konservative Planungsreserve, kein gemessener Bedarf).";
        return "Disk-Puffer rechnerisch vorhanden, tatsächlichen freien Platz vor dem Bootstrap prüfen.";
    }
}

