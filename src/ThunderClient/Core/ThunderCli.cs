using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed class ThunderSshKeyApiTimeoutException : InvalidOperationException
{
    public ThunderSshKeyApiTimeoutException(int exitCode)
        : base("Thunder hat die Registrierung des SSH-Schlüssels nicht innerhalb des " +
            "API-Zeitlimits beantwortet (tnr connect, Exitcode " + exitCode +
            ", POST /v1/instances/{id}/add_key). " +
            "Der Zustand dieser Anfrage ist unbekannt: Der Schlüssel könnte " +
            "serverseitig schon registriert worden sein, wurde von der CLI " +
            "aber nicht lokal gespeichert. Kein neuer Pod ist nötig. " +
            "Die Registrierung wird NICHT automatisch wiederholt.")
    { }
}

public sealed record SnapshotItem(string Id, string Name, string Status, int MinimumDiskGb)
{
    public string Display => $"{Name} · {Status} · mindestens {MinimumDiskGb} GB";
}

public sealed class ThunderCli(ICommandRunner commands)
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMinutes(2);
    // One connection-preparation operation at a time. tnr connect --json may
    // provision SSH credentials for a pod if its key is missing.
    private readonly SemaphoreSlim _connectGate = new(1, 1);

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
                Text(x, "ip"), Int(x, "port"), created, Int(x, "storage"), Int(x, "numGpus"));
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

    public static bool IsCertainCapacityFailure(string response)
    {
        if (Regex.IsMatch(response,
            @"(?i)(payment|authenticat|unauthoriz|forbidden|timeout|timed out|network|connection|quota|billing|invalid token|insufficient balance)",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) return false;
        return Regex.IsMatch(response,
            @"(?i)(out of GPU capacity|GPU configuration\s+[a-zA-Z0-9_-]+\s+x\d+\s+is currently unavailable)",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    // The successful CLI creation response is authoritative for identity.
    // Never log raw JSON: the API may include private credential material.
    public static (string Id, string Uuid) ParseCreateIdentity(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new FormatException("Thunder-Create-Antwort enthält kein Objekt.");
        var id = Text(root, "identifier");
        if (string.IsNullOrWhiteSpace(id)) id = Text(root, "id");
        var uuid = Text(root, "uuid");
        if (!Regex.IsMatch(id, @"^[A-Za-z0-9_-]{1,64}$") ||
            (!string.IsNullOrEmpty(uuid) &&
             !Regex.IsMatch(uuid, @"^[A-Za-z0-9_-]{1,128}$")))
            throw new FormatException(
                "Thunder meldet Create-Erfolg ohne gültige Instanzidentität. " +
                "Vor einem weiteren Create vorhandene Instanzen kontrollieren.");
        return (id, uuid);
    }

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
                "create", "--json", "--gpu", ThunderGpuNames.ToProviderKey(gpu.Key),
                "--num-gpus", gpuCount.ToString(),
                "--vcpus", gpu.Vcpus.ToString(), "--template", template,
                "--disk", diskGb.ToString()], TimeSpan.FromMinutes(5), ct);
            if (output.Success)
            {
                var (id, uuid) = ParseCreateIdentity(output.Stdout);
                // A just-created paid instance need not be visible in status
                // immediately. Its exact identifier comes from create itself.
                IReadOnlyList<InstanceItem> newest;
                try
                {
                    newest = await ListAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException &&
                                          !ct.IsCancellationRequested)
                {
                    // Status failures AFTER successful billable creation must
                    // never destroy its authoritative create identity.
                    log("Create erfolgreich: Pod-ID gespeichert; Statusliste vorübergehend nicht erreichbar.");
                    return new InstanceItem(id, uuid, "", "CREATING",
                        gpu.Key, "", 0, DateTimeOffset.UtcNow, diskGb, gpuCount);
                }
                var matches = newest.Where(item => item.Id == id ||
                    (!string.IsNullOrEmpty(uuid) && item.Uuid == uuid)).ToArray();
                if (matches.Length > 1)
                    throw new InvalidOperationException(
                        "Thunder meldet widersprüchliche Instanzidentitäten. " +
                        "Kein zweiter Pod wird erstellt.");
                if (matches.Length == 1) return matches[0];
                log("Create erfolgreich: exakte Pod-ID gesichert; Thunder-Status wird nachgeführt.");
                return new InstanceItem(id, uuid, "", "CREATING",
                    gpu.Key, "", 0, DateTimeOffset.UtcNow, diskGb, gpuCount);
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

    /// <summary>
    /// Read-only discovery of already present private keys. Does not call tnr,
    /// create or register a key, or modify any remote Thunder resource.
    /// Explicit BYO-key configuration takes precedence and MUST NOT fall back
    /// to stateful CLI provisioning if the configured file is unavailable.
    /// The actual SSH host fingerprint and private-key authentication are
    /// checked separately by SshService.
    /// </summary>
    public static SshConnection? TryExistingSshKey(InstanceItem instance,
        string? configuredKeyPath, string? thunderHome)
    {
        if (instance.Status != "RUNNING")
            throw new InvalidOperationException("SSH nur für RUNNING-Instanzen.");
        if (string.IsNullOrWhiteSpace(instance.Id) ||
            !System.Text.RegularExpressions.Regex.IsMatch(instance.Id,
                @"\A[A-Za-z0-9_-]{1,64}\z") ||
            !System.Net.IPAddress.TryParse(instance.Ip, out _) ||
            instance.Port is < 1 or > 65535)
            throw new InvalidOperationException(
                "Thunder-Status enthält keinen gültigen SSH-Endpunkt (ID, IP, Port).");

        if (!string.IsNullOrWhiteSpace(configuredKeyPath))
        {
            var path = configuredKeyPath.Trim();
            if (!Path.IsPathFullyQualified(path) || path.EndsWith(".pub",
                StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidOperationException(
                    "THUNDERCLIENT_SSH_KEY muss auf eine vorhandene, absolute " +
                    "PRIVATE SSH-Schlüsseldatei zeigen. " +
                    "Keine Thunder-Key-Registrierung wird als Ersatz ausgelöst.");
            return new SshConnection(instance.Id, instance.Ip, instance.Port, path);
        }

        if (string.IsNullOrWhiteSpace(instance.Uuid) ||
            !System.Text.RegularExpressions.Regex.IsMatch(instance.Uuid,
                @"\A[A-Za-z0-9_-]{8,128}\z"))
            return null;

        var home = string.IsNullOrWhiteSpace(thunderHome)
            ? Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), ".thunder")
            : thunderHome;
        if (!Path.IsPathFullyQualified(home)) return null;
        var key = Path.Combine(home, "keys", instance.Uuid);
        return File.Exists(key)
            ? new SshConnection(instance.Id, instance.Ip, instance.Port, key)
            : null;
    }

    public SshConnection? GetExistingSsh(InstanceItem instance) =>
        TryExistingSshKey(instance,
            Environment.GetEnvironmentVariable("THUNDERCLIENT_SSH_KEY"),
            Environment.GetEnvironmentVariable("TNR_HOME"));

    /// <summary>
    /// The official CLI may create and persist a private SSH key if none exists.
    /// Call only after an explicit user action, never as a read-only readiness probe.
    /// </summary>
    public async Task<SshConnection> GetSshAsync(InstanceItem instance, CancellationToken ct = default)
    {
        if (instance.Status != "RUNNING")
            throw new InvalidOperationException("SSH nur für RUNNING-Instanzen.");
        if (string.IsNullOrWhiteSpace(instance.Id))
            throw new InvalidOperationException("Keine gültige Thunder-Instanz-ID vorhanden.");

        // A private key already present on this PC is sufficient to use
        // standard SSH; tnr connect can issue a NON-IDEMPOTENT POST add_key.
        // Never make that Thunder API request when a key is already available.
        var existing = GetExistingSsh(instance);
        if (existing is not null) return existing;

        await _connectGate.WaitAsync(ct);
        try
        {
            // Another concurrent caller may have just created a local key.
            existing = GetExistingSsh(instance);
            if (existing is not null) return existing;
            var response = await CallAsync(["connect", instance.Id, "--json"], ct: ct);
            if (!response.Success)
            {
                if (IsSshKeyRegistrationTimeout(response))
                    throw new ThunderSshKeyApiTimeoutException(response.ExitCode);
                throw new InvalidOperationException(ExplainSshConnectionFailure(response));
            }

            using var doc = JsonDocument.Parse(response.Stdout.Trim());
            var data = doc.RootElement;
            var host = Text(data, "ip");
            var keyFile = Text(data, "key_file");
            var port = Int(data, "port");
            if (string.IsNullOrEmpty(host) || host.IndexOfAny([' ', '\r', '\n', '/', '\\']) >= 0 ||
                !System.Net.IPAddress.TryParse(host, out _) ||
                port is < 1 or > 65535 || !Path.IsPathFullyQualified(keyFile) ||
                !File.Exists(keyFile))
                throw new FormatException(
                    "Thunder lieferte keine vollständigen SSH-Verbindungsparameter " +
                    "(IP, Port und lokale private Schlüsseldatei). " +
                    "Unter Instanzen den SSH-Port prüfen und ggf. CLI-Anmeldung erneuern.");
            return new SshConnection(instance.Id, host, port, keyFile);
        }
        finally { _connectGate.Release(); }
    }

    /// <summary>
    /// --json errors from tnr are reported as {"error":"..."} on stdout.
    /// A CLI usage page can also appear on stderr; it is not the root cause.
    /// Never display raw structured output which may contain sensitive fields.
    /// </summary>
    public static bool IsSshKeyRegistrationTimeout(CommandResult output)
    {
        try
        {
            using var doc = JsonDocument.Parse(output.Stdout.Trim());
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("error", out var err) ||
                err.ValueKind != JsonValueKind.String)
                return false;
            var reason = err.GetString() ?? "";
            return reason.Contains("failed to add SSH key", StringComparison.OrdinalIgnoreCase) &&
                reason.Contains("/add_key", StringComparison.OrdinalIgnoreCase) &&
                (reason.Contains("context deadline exceeded", StringComparison.OrdinalIgnoreCase) ||
                 reason.Contains("Client.Timeout exceeded", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException) { return false; }
    }

    public static string ExplainSshConnectionFailure(CommandResult output)
    {
        if (IsSshKeyRegistrationTimeout(output))
            return new ThunderSshKeyApiTimeoutException(output.ExitCode).Message;
        string? reason = null;
        try
        {
            using var doc = JsonDocument.Parse(output.Stdout.Trim());
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var err) &&
                err.ValueKind == JsonValueKind.String)
                reason = err.GetString();
        }
        catch (JsonException) { /* CLI might be older and not emit structured errors. */ }

        if (string.IsNullOrWhiteSpace(reason))
        {
            var lines = output.Stderr.Split(['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            reason = lines.FirstOrDefault(line =>
                line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("failed to ", StringComparison.OrdinalIgnoreCase));
        }
        var detail = string.IsNullOrWhiteSpace(reason)
            ? "Die CLI lieferte nur Fortschritt bzw. ihre allgemeine Hilfe. " +
              "Der genaue Fehler ist daraus nicht bestimmbar."
            : CleanCliError(reason);

        return "SSH-Einrichtung über die offizielle Thunder-CLI fehlgeschlagen " +
            "(tnr connect --json, Exitcode " + output.ExitCode + "). " + detail +
            " Der SSH-Port und die ComfyUI-Freigabe sind getrennte Themen. " +
            "Ein neuer SSH-Schlüssel wird durch tnr connect ggf. erst erstellt. " +
            "Bitte CLI-Anmeldung, SSH-Schlüsselrechte und Instanzzugang prüfen; " +
            "keine weiteren Pods zur Fehlersuche erstellen.";
    }

    private static string CleanCliError(string text)
    {
        var compact = Regex.Replace(text, @"\x1B\[[0-9;]*[A-Za-z]", "");
        compact = new string(compact.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
        compact = Regex.Replace(compact,
            @"(?i)(?:github_pat_|ghp_|gho_|hf_)[a-z0-9_-]{8,}",
            "[TOKEN MASKIERT]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        compact = Regex.Replace(compact,
            @"(?i)bearer\s+[a-z0-9._~+/=-]+",
            "Bearer [TOKEN MASKIERT]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return compact[..Math.Min(360, compact.Length)].Trim();
    }

    public async Task CreateSnapshotAsync(InstanceItem instance, string name, CancellationToken ct = default)
    {
        if (instance.Status != "RUNNING") throw new InvalidOperationException("Snapshot erfordert eine laufende Instanz.");
        if (!Regex.IsMatch(name, @"^[a-zA-Z0-9_.-]{3,50}$"))
            throw new ArgumentException("Snapshot-Name: 3 bis 50 Buchstaben, Zahlen, Punkt, Unterstrich oder Bindestrich.");
        await JsonAsync(["snapshot", "create", "--instance-id", instance.Id, "--name", name, "--json"], ct);
    }

    /// <summary>
    /// Request a recovery snapshot after a fresh, read-only identity/state check.
    /// This never calls create/delete/modify/reboot. A READY snapshot must be
    /// verified separately before the user restores into a NEW instance.
    /// </summary>
    public async Task RequestRecoverySnapshotAsync(InstanceItem selected, string name,
        CancellationToken ct = default)
    {
        var current = await ListAsync(ct);
        var instance = PodRecoveryPlanner.RequireSameRunningInstance(selected, current);
        await CreateSnapshotAsync(instance, name, ct);
    }

    public async Task DeleteAsync(InstanceItem instance, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instance.Id)) throw new ArgumentException("Instanz-ID fehlt.");
        await JsonAsync(["delete", instance.Id, "--yes", "--json"], ct);
    }
}

