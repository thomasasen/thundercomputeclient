using System.Net;
using System.Text.Json;

namespace ThunderClient.Core;

/// <summary>
/// Read-only, fail-closed quotation from the same /v2/pricing and /v2/specs
/// endpoints used by the official Thunder CLI. No API tokens are supplied.
/// This is an estimate, not a server-enforced billing cap.
/// </summary>
public sealed class ThunderPublicPricing(HttpClient http)
{
    public const string ApiOrigin = "https://api.thundercompute.com:8443/";
    public sealed record Quote(string GpuKey, decimal HourlyUsd, int Vcpus, int DiskGb);

    public async Task<IReadOnlyDictionary<string, Quote>> QuoteAsync(
        IReadOnlyList<GpuChoice> choices, int gpuCount, int diskGb,
        CancellationToken ct = default)
    {
        if (choices.Count == 0 || gpuCount < 1 || diskGb < 1)
            throw new InvalidOperationException("Keine gültige Hardware zur Live-Preisprüfung.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(18));
        using var pricing = await FetchAsync("v2/pricing", deadline.Token);
        using var specs = await FetchAsync("v2/specs", deadline.Token);
        if (!pricing.RootElement.TryGetProperty("pricing", out var rates) ||
            rates.ValueKind != JsonValueKind.Object ||
            !specs.RootElement.TryGetProperty("specs", out var configurations) ||
            configurations.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                "Thunder hat kein vollständiges v2-Preis-/Spezifikationsschema geliefert.");

        static decimal Rate(JsonElement rates, string key)
        {
            if (!rates.TryGetProperty(key, out var value) ||
                !value.TryGetDecimal(out var cost) || cost <= 0)
                throw new InvalidDataException(
                    "Thunder-Preisangabe unvollständig oder nicht positiv: " + key);
            return cost;
        }

        var cpuRate = Rate(rates, "additional_vcpus");
        var diskRate = Rate(rates, "disk_gb");
        var result = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);
        foreach (var gpu in choices)
        {
            var key = gpu.Key.ToLowerInvariant() + "_x" + gpuCount;
            if (!configurations.TryGetProperty(key, out var spec) ||
                spec.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    "Thunder-Spezifikation für GPU-Konfiguration fehlt: " + key);
            if (!spec.TryGetProperty("vcpuOptions", out var options) ||
                options.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Thunder-vCPU-Optionen fehlen: " + key);
            var cpuChoices = options.EnumerateArray()
                .Where(x => x.TryGetInt32(out _)).Select(x => x.GetInt32()).ToArray();
            if (cpuChoices.Length == 0 || !cpuChoices.Contains(gpu.Vcpus))
                throw new InvalidDataException(
                    "Die gewählten " + gpu.Vcpus + " vCPUs sind laut Thunder-Spezifikation nicht zulässig: " + key);
            if (!spec.TryGetProperty("storageGB", out var storage) ||
                !storage.TryGetProperty("min", out var diskMin) ||
                !storage.TryGetProperty("max", out var diskMax) ||
                !diskMin.TryGetInt32(out var min) ||
                !diskMax.TryGetInt32(out var max) ||
                diskGb < min || diskGb > max)
                throw new InvalidDataException(
                    "Die Diskgröße ist laut Thunder-Spezifikation nicht zulässig: " + key);
            if (!spec.TryGetProperty("vramGB", out var vram) ||
                !vram.TryGetInt32(out var vramGb) || vramGb < gpu.VramGb)
                throw new InvalidDataException(
                    "Die Anbieter-Spezifikation bestätigt nicht die erforderlichen " +
                    gpu.VramGb + " GB VRAM: " + key);
            var providerPrice = Rate(rates, key) +
                Math.Max(0, gpu.Vcpus - cpuChoices.Min()) * cpuRate +
                Math.Max(0, diskGb - 100 * gpuCount) * diskRate;
            // Never let a stale, incorrect, or user-edited local number
            // override the higher of provider and published conservative rates.
            var approved = Math.Max(providerPrice,
                Estimates.Hourly(gpu, gpuCount, diskGb));
            result.Add(gpu.Key, new Quote(gpu.Key, approved, gpu.Vcpus, diskGb));
        }
        return result;
    }

    private async Task<JsonDocument> FetchAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiOrigin + path);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException(
                "Thunder verweigert die anonyme Preisabfrage. Kostenpflichtiger Create ist " +
                "ohne bestätigten Live-Preis gesperrt; keine Zugangsdaten in URLs angeben.");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var bounded = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (bounded.Length + count > 200_000)
                throw new InvalidDataException("Thunder-Preis-/Spec-Antwort ist unerwartet groß.");
            bounded.Write(buffer, 0, count);
        }
        bounded.Position = 0;
        return await JsonDocument.ParseAsync(bounded, cancellationToken: ct);
    }
}
