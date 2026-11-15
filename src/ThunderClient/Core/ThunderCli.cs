using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed record SnapshotItem(string Id, string Name, string Status, int MinimumDiskGb)
{
    public string Display => $"{Name} · {Status} · mindestens {MinimumDiskGb} GB";
}

public sealed class ThunderCli(ICommandRunner commands)
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMinutes(2);

    private async Task<CommandResult> CallAsync(string[] args, TimeSpan? timeout = null,
        CancellationToken ct = default)
        => await commands.RunAsync("tnr", args, timeout ?? ShortTimeout, cancellationToken: ct);

    private async Task<string> JsonAsync(string[] args, CancellationToken ct = default)
    {
        var result = await CallAsync(args, ct: ct);
        if (!result.Success) throw new ExternalCommandException("tnr", result);
        return result.Stdout.Trim();
    }

    private static string Text(JsonElement obj, string field)
        => obj.ValueKind == JsonValueKind.Object &&
           obj.TryGetProperty(field, out var val) && val.ValueKind != JsonValueKind.Null
           ? val.ToString() : "";

    private static int Int(JsonElement obj, string field)
        => int.TryParse(Text(obj, field), out var n) ? n : 0;

    public static IReadOnlyList<InstanceItem> ParseInstances(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Null) return [];
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new FormatException("Thunder-Instanzstatus enthält kein JSON-Array.");
        return doc.RootElement.EnumerateArray().Select(x =>
        {
            DateTimeOffset? created = DateTimeOffset.TryParse(Text(x, "createdAt"), out var d) ? d : null;
            return new InstanceItem(Text(x, "id"), Text(x, "uuid"),
                Text(x, "name"), Text(x, "status"), Text(x, "gpuType"),
                Text(x, "ip"), Int(x, "port"), created);
        }).ToArray();
    }

    public Task<IReadOnlyList<InstanceItem>> ListAsync(CancellationToken ct = default)
        => ListCoreAsync(ct);

    private async Task<IReadOnlyList<InstanceItem>> ListCoreAsync(CancellationToken ct)
        => ParseInstances(await JsonAsync(["status", "--json", "--no-wait"], ct));

    public async Task<IReadOnlyList<SnapshotItem>> ListSnapshotsAsync(CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(await JsonAsync(["snapshot", "list", "--json", "--no-wait"], ct));
        if (doc.RootElement.ValueKind == JsonValueKind.Null) return [];
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Ungültige Snapshot-JSON-Antwort.");
        return doc.RootElement.EnumerateArray().Select(x => new SnapshotItem(
            Text(x, "id"), Text(x, "name"), Text(x, "status"), Int(x, "minimumDiskSizeGb"))).ToArray();
    }

    public static bool IsCertainCapacityFailure(string response) =>
        Regex.IsMatch(response,
            @"(?i)(out of GPU capacity|GPU configuration\s+[a-zA-Z0-9_-]+\s+x\d+\s+is currently unavailable)",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    // Das Sperrfile verhindert parallele Creates in zwei Client-Prozessen desselben Windows-Accounts.
    public async Task<InstanceItem> CreateWithFallbackAsync(
        IReadOnlyList<GpuChoice> priority, int gpuCount, int diskGb,
        string template, decimal approvedEstimatedHourlyMax,
        string lockFolder, Action<string> log, CancellationToken ct = default)
    {
        Directory.CreateDirectory(lockFolder);
        using var guard = new FileStream(Path.Combine(lockFolder, "create.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var before = await ListAsync(ct);
        if (before.Count > 0)
            throw new InvalidOperationException("Thunder meldet bereits mindestens eine Instanz. Automatische Erstellung gesperrt, damit keine Duplikate entstehen. Bestehende Instanz verbinden oder manuell prüfen.");

        foreach (var gpu in priority.Where(x => x.Enabled))
        {
            var rate = Estimates.Hourly(gpu, gpuCount, diskGb);
            if (gpu.Vcpus <= 0 || gpuCount < 1 || diskGb < 1)
                throw new InvalidOperationException("Ungültige GPU-/vCPU-/Disk-Konfiguration.");
            if (rate > approvedEstimatedHourlyMax)
            {
                log($"{gpu.Key}: geschätzter Listenpreis {rate:N3} USD/h über Preisgrenze, übersprungen.");
                continue;
            }
            log($"GPU-Createversuch: {gpu.Key}, {gpu.Vcpus} vCPU, {gpuCount} GPU(s), {diskGb} GB.");
            var output = await CallAsync([
                "create", "--json", "--gpu", gpu.Key, "--num-gpus", gpuCount.ToString(),
                "--vcpus", gpu.Vcpus.ToString(), "--template", template,
                "--disk", diskGb.ToString()], TimeSpan.FromMinutes(5), ct);
            if (output.Success)
            {
                var newest = await ListAsync(ct);
                if (newest.Count != 1)
                    throw new InvalidOperationException("Create war erfolgreich, aber der neue Instanzstatus ist nicht eindeutig. Vor weiteren Aktionen manuell prüfen.");
                return newest[0];
            }
            // Unabhängig vom Rückgabecode könnte die API bereits eine Instanz angelegt haben.
            var after = await ListAsync(ct);
            if (after.Count != 0)
                throw new InvalidOperationException("Create lieferte einen Fehler, Thunder meldet jedoch eine Instanz. Kein automatischer Wiederholungsversuch.");
            if (!IsCertainCapacityFailure(output.Combined))
                throw new ExternalCommandException("tnr create (kein nachgewiesener Kapazitätsfehler)", output);
            log($"{gpu.Key}: eindeutig erkannter Kapazitätsfehler. Nächste freigegebene GPU wird geprüft.");
        }
        throw new InvalidOperationException("Keine geeignete GPU innerhalb der freigegebenen Schätzpreisgrenze verfügbar.");
    }

    public async Task<SshConnection> GetSshAsync(InstanceItem instance, CancellationToken ct = default)
    {
        if (instance.Status != "RUNNING") throw new InvalidOperationException("SSH nur für RUNNING-Instanzen.");
        using var doc = JsonDocument.Parse(await JsonAsync(["connect", instance.Id, "--json"], ct));
        var data = doc.RootElement;
        var host = Text(data, "ip");
        var keyFile = Text(data, "key_file");
        var port = Int(data, "port");
        if (string.IsNullOrEmpty(host) || host.IndexOfAny([' ', '\r', '\n', '/', '\\']) >= 0 ||
            port is < 1 or > 65535 || !Path.IsPathFullyQualified(keyFile) || !File.Exists(keyFile))
            throw new FormatException("Thunder lieferte keine vollständigen vertrauenswürdigen SSH-Verbindungsparameter.");
        return new SshConnection(instance.Id, host, port, keyFile);
    }

    public async Task CreateSnapshotAsync(InstanceItem instance, string name, CancellationToken ct = default)
    {
        if (instance.Status != "RUNNING") throw new InvalidOperationException("Snapshot erfordert eine laufende Instanz.");
        if (!Regex.IsMatch(name, @"^[a-zA-Z0-9_.-]{3,50}$"))
            throw new ArgumentException("Snapshot-Name: 3 bis 50 Buchstaben, Zahlen, Punkt, Unterstrich oder Bindestrich.");
        await JsonAsync(["snapshot", "create", "--instance-id", instance.Id, "--name", name, "--json"], ct);
    }

    public async Task DeleteAsync(InstanceItem instance, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instance.Id)) throw new ArgumentException("Instanz-ID fehlt.");
        await JsonAsync(["delete", instance.Id, "--yes", "--json"], ct);
    }
}

