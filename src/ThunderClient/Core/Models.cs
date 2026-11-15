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
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
    public string Canvases => string.Join(", ", CanvasFiles);
    public string ModelCount => Models.Count.ToString(CultureInfo.InvariantCulture);
    public string ModelSize => Models.All(x => x.SizeGb.HasValue)
        ? $"{Models.Sum(x => x.SizeGb!.Value):N2} GB" : "unbekannt / unvollständig";
    public string Weights => string.Join(", ", Models.Take(12).Select(x => x.Filename)) +
        (Models.Count > 12 ? $" … (+{Models.Count - 12})" : "");
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
    public decimal MaximumHourlyUsd { get; set; } = 0.85m;
    public string Template { get; set; } = "base";
    public string Theme { get; set; } = "Hell";
    public bool OfflineApproved { get; set; }
    public string LocalGitRef { get; set; } = "main";
    public List<string> SelectedWorkflows { get; set; } = [];
    public List<string> GpuOrder { get; set; } = ["a6000", "l40", "a100"];
}

public static class Estimates
{
    // Historical published list estimates from 2026-11-15, NOT live provider quotations.
    public const decimal AdditionalDiskGbMonthUsd = 0.219m;
    public static decimal Hourly(GpuChoice gpu, int gpuCount, int diskGb) =>
        gpu.GpuHourlyEstimate * gpuCount +
        Math.Max(0, diskGb - 100 * gpuCount) * AdditionalDiskGbMonthUsd / 730m;
    public static string CapacityWarning(double? manifestGb, int diskGb)
    {
        if (!manifestGb.HasValue) return "Manifestgröße nicht vollständig bekannt. Disk manuell prüfen.";
        if (diskGb < manifestGb.Value + 40)
            return $"WARNUNG: {diskGb} GB Gesamtdisk bei {manifestGb:N2} GB Modellen. Betriebssystem, venvs, Cache und Outputs benötigen zusätzlich Platz (40 GB nur konservative Planungsreserve, kein gemessener Bedarf).";
        return "Disk-Puffer rechnerisch vorhanden, tatsächlichen freien Platz vor dem Bootstrap prüfen.";
    }
}

