using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

/// <summary>
/// API-verified SSH-first access. The existing private key never leaves the
/// Windows user profile. Only its derived public half is POSTed to the
/// documented Thunder endpoint, with idempotency guaranteed by the provider.
/// Never fall back to tnr connect's server-generated private-key mode.
/// </summary>
public sealed class ThunderIdempotentSshAccess(HttpClient http, ICommandRunner runner)
{
    public const string EndpointBase = "https://api.thundercompute.com:8443";
    private const int KeyRequestAttempts = 2;
    public sealed record LocalIdentity(string PrivateFile, string PublicKey);
    public sealed record AuthorizationResult(SshConnection Endpoint, bool ProviderConfirmed);

    private static bool IsSafePath(string file)
    {
        if (!Path.IsPathFullyQualified(file) || file.EndsWith(".pub",
                StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
            return false;
        return !File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint);
    }

    public async Task<LocalIdentity> PrepareIdentityAsync(
        string? customPrivateKey, CancellationToken ct = default)
    {
        string path;
        if (!string.IsNullOrWhiteSpace(customPrivateKey))
        {
            path = customPrivateKey.Trim();
            if (!IsSafePath(path))
                throw new InvalidOperationException(
                    "THUNDERCLIENT_SSH_KEY ist keine gültige vorhandene private Schlüsseldatei. " +
                    "Kostenpflichtige Pod-Erstellung gesperrt.");
        }
        else
        {
            var folder = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "ThunderClient", "ssh");
            if (!Path.IsPathFullyQualified(folder))
                throw new InvalidOperationException("Ungültiges lokales Schlüsselverzeichnis.");
            Directory.CreateDirectory(folder);
            if (File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("SSH-Schlüsselverzeichnis ist ein Symlink. Abbruch.");
            path = Path.Combine(folder, "identity_ed25519");
            if (File.Exists(path + ".pub") && !File.Exists(path))
                throw new InvalidOperationException(
                    "Es existiert nur der öffentliche Schlüssel. Kein neues Schlüsselpaar " +
                    "über vorhandene Dateien schreiben.");
            if (!File.Exists(path))
            {
                var result = await runner.RunAsync("ssh-keygen",
                    ["-q", "-t", "ed25519", "-N", "", "-f", path,
                     "-C", "ThunderClient"], TimeSpan.FromSeconds(20),
                    cancellationToken: ct);
                if (!result.Success || !IsSafePath(path))
                    throw new InvalidOperationException(
                        "Lokaler SSH-Schlüssel konnte nicht sicher angelegt werden. " +
                        "Kein kostenpflichtiger Pod wird gestartet.");
            }
            if (!IsSafePath(path))
                throw new InvalidOperationException(
                    "SSH-Schlüssel ist nicht lesbar oder nicht sicher gespeichert.");
        }

        // ssh-keygen -y verifies the private key, including a possible passphrase,
        // and derives the public key. Never trust a separately edited .pub file.
        var derived = await runner.RunAsync("ssh-keygen",
            ["-y", "-f", path], TimeSpan.FromSeconds(20),
            cancellationToken: ct);
        if (!derived.Success)
            throw new InvalidOperationException(
                "Privater SSH-Schlüssel kann nicht geprüft werden. " +
                "Passphrase/Dateizugriff untersuchen, kein Create.");
        var publicKey = NormalizePublicKey(derived.Stdout);
        return new LocalIdentity(path, publicKey);
    }

    public static string NormalizePublicKey(string key)
    {
        var parts = key.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] != "ssh-ed25519" ||
            parts[1].Length is < 60 or > 1024 ||
            !Regex.IsMatch(parts[1], @"\A[A-Za-z0-9+/]+={0,2}\z"))
            throw new InvalidDataException(
                "Thunder benötigt einen gültigen ed25519-SSH-Public-Key.");
        // Drop potentially identifying key comments. A stable byte sequence
        // also guarantees the identical idempotency key on every retry.
        return parts[0] + " " + parts[1];
    }

    public async Task ValidateTokenAsync(string apiToken, CancellationToken ct = default)
    {
        ValidateTokenPresent(apiToken);
        using var request = Request(HttpMethod.Get, "/v1/instances/list", apiToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        using HttpResponseMessage response;
        try { response = await SendAsync(request, deadline.Token); }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(
                "Thunder-API ist nicht erreichbar. Kein kostenpflichtiger Create.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Thunder-API-Lesetest hat das Zeitlimit überschritten. Kein kostenpflichtiger Create.");
        }
        if (!response.IsSuccessStatusCode)
            throw ResponseFailure(response.StatusCode, "Thunder-API-Lesetest");
        // A valid named Thunder token must access the documented instance list.
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(deadline.Token),
            cancellationToken: deadline.Token);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                "Thunder-API lieferte keine Instanzliste; kostenpflichtiger Create gesperrt.");
    }

    public async Task<AuthorizationResult> AuthorizeAsync(InstanceItem instance,
        LocalIdentity identity, string apiToken, CancellationToken ct = default)
    {
        ValidateTokenPresent(apiToken);
        if (instance.Status != "RUNNING" || !Regex.IsMatch(instance.Id,
                @"\A[A-Za-z0-9_-]{1,64}\z") ||
            !IPAddress.TryParse(instance.Ip, out _) || instance.Port is < 1 or > 65535 ||
            !IsSafePath(identity.PrivateFile))
            throw new InvalidOperationException(
                "SSH-Public-Key wird nur für einen verifizierten RUNNING-Pod " +
                "mit existierender privater Schlüsseldatei registriert.");
        // Revalidate, so the public_key body is ALWAYS included.
        var publicKey = NormalizePublicKey(identity.PublicKey);
        var body = JsonSerializer.Serialize(new { public_key = publicKey });
        var path = "/v1/instances/" + instance.Id + "/add_key";
        for (var attempt = 1; attempt <= KeyRequestAttempts; attempt++)
        {
            using var request = Request(HttpMethod.Post, path, apiToken);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(35));
            try
            {
                using var response = await SendAsync(request, deadline.Token);
                if (response.StatusCode is HttpStatusCode.RequestTimeout or
                    HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or
                    HttpStatusCode.GatewayTimeout ||
                    (int)response.StatusCode >= 500)
                {
                    if (attempt < KeyRequestAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        continue;
                    }
                    // Provider may have applied the key before sending 5xx.
                    // The caller MUST verify actual SSH authentication before
                    // reporting success. Never fall back to tnr connect.
                    return new AuthorizationResult(
                        new SshConnection(instance.Id, instance.Ip, instance.Port, identity.PrivateFile),
                        false);
                }
                if (!response.IsSuccessStatusCode)
                    throw ResponseFailure(response.StatusCode, "Thunder-SSH-Public-Key");
                using var payload = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(deadline.Token),
                    cancellationToken: deadline.Token);
                var data = payload.RootElement;
                if (data.ValueKind != JsonValueKind.Object ||
                    !data.TryGetProperty("success", out var success) ||
                    success.ValueKind != JsonValueKind.True ||
                    (data.TryGetProperty("key", out var returnedKey) &&
                     returnedKey.ValueKind == JsonValueKind.String &&
                     !string.IsNullOrEmpty(returnedKey.GetString())))
                    throw new InvalidDataException(
                        "Thunder bestätigt keinen idempotenten Public-Key-Modus. " +
                        "Keine geheimen API-Antworten protokolliert.");
                if (data.TryGetProperty("uuid", out var uuid) &&
                    uuid.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(instance.Uuid) &&
                    !string.Equals(uuid.GetString(), instance.Uuid, StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Thunder bestätigt eine andere Instanz-UUID. SSH-Anmeldung gesperrt.");
                return new AuthorizationResult(
                    new SshConnection(instance.Id, instance.Ip, instance.Port, identity.PrivateFile),
                    true);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // With public_key the provider explicitly documents no-op
                // semantics for duplicates. Retry ONLY exactly the same key.
                if (attempt == KeyRequestAttempts) break;
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
            catch (HttpRequestException) when (!ct.IsCancellationRequested)
            {
                // Same immutable public_key for the only retry.
                if (attempt == KeyRequestAttempts) break;
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
        // Timeout/transport failure is ambiguous but not a reason to give
        // up: with the same local private key, a strictly authenticated SSH
        // probe can prove whether the server already attached the public half.
        return new AuthorizationResult(
            new SshConnection(instance.Id, instance.Ip, instance.Port, identity.PrivateFile),
            false);
    }

    private static void ValidateTokenPresent(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Contains('\r') || token.Contains('\n'))
            throw new InvalidOperationException(
                "Thunder-API-Token fehlt oder ist ungültig. Unter Zugangsdaten " +
                "einen benannten API-Token aus der Thunder Console speichern.");
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, EndpointBase + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        // NEVER show exception detail from HttpRequestException; some HTTP
        // transports include URLs, raw request headers or private fields.
        => http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

    private static InvalidOperationException ResponseFailure(
        HttpStatusCode status, string operation)
        => new(operation + " meldet HTTP " + (int)status +
            ". Token, Account, Berechtigungen und Instanzzuordnung prüfen. " +
            "Keine erneute Generierung eines SSH-Schlüssels.");
}
