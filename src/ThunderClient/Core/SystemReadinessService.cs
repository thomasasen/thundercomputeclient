using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;

namespace ThunderClient.Core;

/// <summary>Readiness, never evidence of GPU compatibility or billing safety.</summary>
public sealed class SetupCheck : Bindable
{
    private string _status = "Nicht geprüft";
    private string _detail = "Über „Jetzt prüfen“ einen lesenden Test starten.";
    private string _testedAt = "Noch kein Test durchgeführt";
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Guide { get; init; }
    public required string HelpUrl { get; init; }
    public required string HelpLabel { get; init; }
    public int SetupTabIndex { get; init; } = -1;
    public bool Optional { get; init; }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string Detail { get => _detail; private set => Set(ref _detail, value); }
    public string TestedAt { get => _testedAt; private set => Set(ref _testedAt, value); }
    public void Update(string status, string detail)
    {
        Status = status;
        Detail = detail;
        TestedAt = status is "Nicht geprüft" or "Prüfe …"
            ? "Noch kein abgeschlossener Test"
            : "Zuletzt geprüft: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
    }
}

public sealed class SystemReadinessService : IDisposable
{
    private readonly ICommandRunner _runner;
    private readonly Func<string, string?> _readSecret;
    private readonly Func<string, CancellationToken, Task<WorkflowSnapshot>> _readPrivateCatalog;
    private readonly Func<InstanceItem?> _selectedPod;
    private readonly SshService _ssh;
    private readonly HttpClient _http;
    private readonly bool _disposeHttp;
    private InstanceItem? _runningPod;
    private SshConnection? _sshEndpoint;

    // Links point only to official account forms, product documentation or vendor sources.
    public static ObservableCollection<SetupCheck> CreateChecks() => new([
        new SetupCheck {
            Id="github", Title="GitHub · privater Workflow-Katalog",
            Guide="Fine-grained PAT anlegen: Resource owner und nur 'runpod-ai-workflows' wählen. Repository permissions → Contents: Read-only. Token unter Zugangsdaten speichern. Kein Schreibzugriff nötig.",
            HelpUrl="https://github.com/settings/personal-access-tokens/new",
            HelpLabel="GitHub-Token erstellen", SetupTabIndex=5
        },
        new SetupCheck {
            Id="thunder", Title="Thunder Compute · CLI und Anmeldung",
            Guide="Offizielle tnr-CLI installieren. In PowerShell 'tnr login' ausführen; danach 'tnr status --json --no-wait'. Der Systemcheck führt ausschließlich die Statusabfrage aus.",
            HelpUrl="https://github.com/Thunder-Compute/thunder-cli/releases/latest",
            HelpLabel="Offizielle CLI herunterladen", SetupTabIndex=5
        },
        new SetupCheck {
            Id="thunder-api", Title="Thunder Compute · eigener API-Token (SSH-Erstzugang)",
            Guide="In der Thunder Console unter Authentication → API Tokens einen eigenen benannten API-Token anlegen und unter Zugangsdaten speichern. Die Prüfung sendet nur GET /v1/instances/list und erstellt weder einen Pod noch einen SSH-Key. Der Ein-Klick-Start verlangt diesen Zugriff vor der kostenpflichtigen Pod-Erstellung, wenn noch kein lokaler SSH-Zugang besteht.",
            HelpUrl="https://console.thundercompute.com/settings/tokens",
            HelpLabel="Thunder-API-Token verwalten", SetupTabIndex=5
        },
        new SetupCheck {
            Id="openssh", Title="Windows OpenSSH · Client-Werkzeuge",
            Guide="In Windows → Einstellungen → System → Optionale Features den OpenSSH-Client installieren. Benötigt werden ssh.exe, ssh-keyscan.exe und ssh-keygen.exe. Kein SSH-Server auf deinem PC erforderlich.",
            HelpUrl="https://learn.microsoft.com/de-de/windows-server/administration/openssh/openssh_install_firstuse",
            HelpLabel="Microsoft-Installationsanleitung", SetupTabIndex=3
        },
        new SetupCheck {
            Id="ssh-key", Title="SSH-Schlüssel · ausgewählter Pod",
            Guide="Laufenden Pod unter Instanzen auswählen. Die rein lesende Prüfung sucht einen ausdrücklich festgelegten privaten SSH-Key, die Client-Identität oder TNR_HOME/keys/UUID. Die eigentliche Zuordnung erfolgt mit public_key idempotent über die dokumentierte Thunder-API; der Desktop-Client ruft nicht mehr tnr connect zur Schlüsselgenerierung auf.",
            HelpUrl="https://github.com/Thunder-Compute/thunder-compute-documentation/blob/main/guides/ssh-on-thunder-compute.mdx",
            HelpLabel="Thunder-SSH-Anleitung", SetupTabIndex=3
        },
        new SetupCheck {
            Id="ssh-auth", Title="SSH-Hostvertrauen und Anmeldung",
            Guide="Hostfingerprint aus einer unabhängigen Thunder-Quelle vergleichen und im Client ausdrücklich bestätigen. Erst danach prüft der Systemcheck eine echte SSH-Anmeldung mit einem harmlosen 'true'-Befehl. Kein Auto-Trust.",
            HelpUrl="https://github.com/Thunder-Compute/thunder-compute-documentation/blob/main/guides/ssh-on-thunder-compute.mdx",
            HelpLabel="SSH und Fingerprint-Hilfe", SetupTabIndex=3
        },
        new SetupCheck {
            Id="huggingface", Title="Hugging Face · Modell-Token",
            Guide="Optional: In Hugging Face → Settings → Access Tokens einen Token mit minimal nötigen Leserechten erstellen und unter Zugangsdaten speichern. Der Test nutzt 'whoami-v2'; Gated-Model-Zugriffe sind damit nicht automatisch bestätigt.",
            HelpUrl="https://huggingface.co/settings/tokens",
            HelpLabel="Hugging-Face-Token erstellen", SetupTabIndex=5, Optional=true
        },
        new SetupCheck {
            Id="civitai", Title="Civitai · Modell-API",
            Guide="Optional: Im Civitai-Konto einen API-Key anlegen, unter Zugangsdaten speichern und über den lesenden '/api/v1/me'-Endpunkt testen. Der Test bestätigt nicht die Downloadberechtigung jedes einzelnen Modells.",
            HelpUrl="https://civitai.com/user/account",
            HelpLabel="Civitai-API-Key erstellen", SetupTabIndex=5, Optional=true
        }
    ]);

    public SystemReadinessService(ICommandRunner runner, Func<string, string?> readSecret,
        Func<string, CancellationToken, Task<WorkflowSnapshot>> readPrivateCatalog,
        Func<InstanceItem?> selectedPod, SshService ssh, HttpClient? http = null)
    {
        _runner = runner;
        _readSecret = readSecret;
        _readPrivateCatalog = readPrivateCatalog;
        _selectedPod = selectedPod;
        _ssh = ssh;
        _disposeHttp = http is null;
        _http = http ?? new HttpClient(new HttpClientHandler {
            AllowAutoRedirect = false, UseCookies = false
        }) { Timeout = TimeSpan.FromSeconds(12) };
    }

    public async Task CheckAllAsync(IList<SetupCheck> checks,
        Action<SetupCheck>? updated = null, CancellationToken ct = default)
    {
        _runningPod = null;
        _sshEndpoint = null;
        foreach (var check in checks)
        {
            ct.ThrowIfCancellationRequested();
            check.Update("Prüfe …", "Lesende Prüfung läuft.");
            updated?.Invoke(check);
            try
            {
                var result = check.Id switch {
                    "github" => await CheckGitHubAsync(ct),
                    "thunder" => await CheckThunderAsync(ct),
                    "thunder-api" => await CheckThunderApiAsync(ct),
                    "openssh" => CheckOpenSsh(),
                    "ssh-key" => await CheckSshKeyAsync(ct),
                    "ssh-auth" => await CheckSshAuthenticationAsync(ct),
                    "huggingface" => await CheckTokenApiAsync("huggingface",
                        "https://huggingface.co/api/whoami-v2", ct),
                    "civitai" => await CheckTokenApiAsync("civitai",
                        "https://civitai.com/api/v1/me", ct),
                    _ => ("Fehler", "Unbekannte Prüfung.")
                };
                check.Update(result.Item1, result.Item2);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                check.Update("Nicht geprüft", "Prüfung abgebrochen.");
                throw;
            }
            catch (Exception ex)
            {
                // Do not copy third-party response bodies, paths, tokens or URLs into the UI.
                check.Update("Fehler", ex switch {
                    HttpRequestException => "Netzwerkfehler: Anbieter derzeit nicht erreichbar.",
                    TaskCanceledException => "Zeitüberschreitung bei der Verbindung.",
                    _ => "Prüfung fehlgeschlagen. Details bitte über den jeweiligen Einrichtungsweg prüfen."
                });
            }
            updated?.Invoke(check);
        }
    }

    private async Task<(string, string)> CheckGitHubAsync(CancellationToken ct)
    {
        var token = _readSecret("github");
        if (string.IsNullOrWhiteSpace(token))
            return ("Fehlt", "Kein GitHub-Lesetoken gespeichert. Token nur für das private Repository anlegen.");
        // FetchAsync verifies the actual private tree and immutable manifests, not merely
        // that a token exists or the API responds with unauthenticated public data.
        try
        {
            var snapshot = await _readPrivateCatalog(token, ct);
            return ("Erfolgreich getestet",
                snapshot.Workflows.Count + " private Workflows gelesen · Revision " +
                snapshot.Commit[..Math.Min(12, snapshot.Commit.Length)] + ".");
        }
        catch (InvalidOperationException)
        {
            return ("Fehler", "Privater GitHub-Zugriff fehlgeschlagen: Token, Ablaufdatum und Contents: Read-only für genau dieses Repository prüfen.");
        }
    }

    private async Task<(string, string)> CheckThunderApiAsync(CancellationToken ct)
    {
        var token = _readSecret("thunder-api");
        if (string.IsNullOrWhiteSpace(token))
            return ("Fehlt", "Kein benannter Thunder-API-Token für die idempotente SSH-Erstregistrierung hinterlegt.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            ThunderIdempotentSshAccess.EndpointBase + "/v1/instances/list");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return ("Fehler", "Thunder-API-Token nicht bestätigt (HTTP " +
                (int)response.StatusCode + "). Token, Workspace und Berechtigungen überprüfen.");
        using var json = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
            ? ("Erfolgreich getestet", "Thunder hat GET /v1/instances/list mit diesem " +
                "API-Token akzeptiert. Keine Instanz oder SSH-Key angelegt.")
            : ("Fehler", "Thunder-API-Instanzliste hat ein unerwartetes Format.");
    }

    private async Task<(string, string)> CheckThunderAsync(CancellationToken ct)
    {
        var result = await _runner.RunAsync("tnr", ["status", "--json", "--no-wait"],
            TimeSpan.FromSeconds(25), cancellationToken: ct);
        if (!result.Success)
            return ("Fehler", "Thunder-Statusabfrage fehlgeschlagen. CLI über 'tnr login' anmelden; auf dem PC 'tnr status --json --no-wait' prüfen.");
        IReadOnlyList<InstanceItem> pods;
        try { pods = ThunderCli.ParseInstances(result.Stdout.Trim()); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException)
        {
            return ("Fehler", "Thunder hat keine gültige Instanzliste zurückgegeben.");
        }
        var selected = _selectedPod();
        _runningPod = selected is { Status: "RUNNING" } &&
                      pods.Any(x => x.Id == selected.Id && x.Status == "RUNNING")
            ? selected : null;
        return ("Erfolgreich getestet",
            "CLI-Statusabfrage authentifiziert · " + pods.Count +
            " Instanz(en). Keine Instanz erstellt und keine Kostenaktion ausgelöst.");
    }

    public static string? FindExecutable(string executable, string? windowsFolder = null,
        string? searchPath = null)
    {
        if (executable is not ("ssh" or "ssh-keyscan" or "ssh-keygen")) return null;
        var native = ProcessRunner.ResolveOpenSshPath(executable, windowsFolder);
        if (Path.IsPathFullyQualified(native) && File.Exists(native)) return native;
        foreach (var directory in (searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "")
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var folder = directory.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(folder)) continue;
            var candidate = Path.Combine(folder, executable + ".exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static (string, string) CheckOpenSsh()
    {
        var missing = new[] { "ssh", "ssh-keyscan", "ssh-keygen" }
            .Where(x => FindExecutable(x) is null).ToArray();
        return missing.Length == 0
            ? ("Vorhanden", "Alle drei OpenSSH-Werkzeuge auf diesem PC gefunden. Netzwerk-/Pod-Verbindung separat prüfen.")
            : ("Fehlt", "OpenSSH-Komponenten fehlen: " + string.Join(", ", missing) +
                ". Windows-OpenSSH-Client installieren.");
    }

    private Task<(string, string)> CheckSshKeyAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _sshEndpoint = null;
        if (_runningPod is null)
            return Task.FromResult(("Nicht prüfbar",
                "Kein zuvor ausgewählter RUNNING-Pod im aktuellen Thunder-Status. " +
                "Pod unter Instanzen auswählen und den Test erneut ausführen."));
        if (FindExecutable("ssh") is null)
            return Task.FromResult(("Fehlt", "Windows-OpenSSH-Client fehlt."));
        if (string.IsNullOrWhiteSpace(_runningPod.Uuid) ||
            !System.Text.RegularExpressions.Regex.IsMatch(_runningPod.Uuid,
                @"\A[a-zA-Z0-9_-]{8,128}\z"))
            return Task.FromResult(("Nicht prüfbar",
                "Thunder liefert keine gültige Instanz-UUID für den lokalen SSH-Schlüsselpfad."));
        if (string.IsNullOrWhiteSpace(_runningPod.Ip) ||
            !System.Net.IPAddress.TryParse(_runningPod.Ip, out _) ||
            _runningPod.Port is < 1 or > 65535)
            return Task.FromResult(("Nicht prüfbar",
                "SSH-IP oder SSH-Port fehlen noch im Thunder-Status. " +
                "Status nach dem Start erneut aktualisieren."));
        // Explicitly configured user-owned key is an opt-in read-only
        // alternative to tnr connect's potentially non-idempotent /add_key.
        var configuredKey = Environment.GetEnvironmentVariable("THUNDERCLIENT_SSH_KEY");
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            try
            {
                _sshEndpoint = ThunderCli.TryExistingSshKey(_runningPod,
                    configuredKey, Environment.GetEnvironmentVariable("TNR_HOME"));
                return Task.FromResult(("Vorhanden",
                    "Explizit gewählter privater SSH-Schlüssel gefunden. " +
                    "Keine Registrierung über tnr connect erforderlich. " +
                    "Hostfingerprint und echte SSH-Anmeldung müssen separat bestätigt werden."));
            }
            catch (InvalidOperationException ex)
            {
                return Task.FromResult(("Fehler", ex.Message));
            }
        }
        // The one-click flow now owns one persistent local ED25519 identity.
        // This inspection is strictly read-only and never provisions a key.
        var appIdentity = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "ThunderClient", "ssh", "identity_ed25519");
        if (File.Exists(appIdentity) &&
            !File.GetAttributes(appIdentity).HasFlag(FileAttributes.ReparsePoint))
        {
            _sshEndpoint = new SshConnection(_runningPod.Id,
                _runningPod.Ip, _runningPod.Port, appIdentity);
            return Task.FromResult(("Vorhanden",
                "Lokal erzeugter ThunderClient-SSH-Schlüssel gefunden. " +
                "Der Public-Key muss diesem Pod über die dokumentierte Thunder-API " +
                "zugeordnet sein; dies wird im SSH-Anmeldetest verifiziert."));
        }
        var thunderHome = Environment.GetEnvironmentVariable("TNR_HOME");
        if (string.IsNullOrWhiteSpace(thunderHome))
            thunderHome = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), ".thunder");
        var keyPath = Path.Combine(thunderHome, "keys", _runningPod.Uuid);
        if (!File.Exists(keyPath))
            return Task.FromResult(("Einrichtung nötig",
                "Am Standardort des Thunder-Clients wurde noch keine private Schlüsseldatei " +
                "gefunden. Der Client legt beim Statuscheck keinen Schlüssel an. " +
                "Ein ausdrücklich gestartetes 'tnr connect' kann eine SSH-Schlüsseleinrichtung " +
                "auslösen. Falls Thunder TNR_HOME oder einen Cache-Fallback nutzt, " +
                "diesen Speicherort separat prüfen."));
        _sshEndpoint = new SshConnection(_runningPod.Id, _runningPod.Ip,
            _runningPod.Port, keyPath);
        return Task.FromResult(("Vorhanden",
            "Private SSH-Schlüsseldatei gefunden (Inhalt nicht gelesen). " +
            "Die tatsächliche Anmeldung wird gesondert getestet."));
    }

    private async Task<(string, string)> CheckSshAuthenticationAsync(CancellationToken ct)
    {
        if (_sshEndpoint is null)
            return ("Nicht prüfbar", "Erst wenn ein laufender Pod und der private Schlüssel verfügbar sind, lässt sich SSH testen.");
        if (!await _ssh.HasKnownHostAsync(_sshEndpoint, ct))
            return ("Einrichtung nötig", "Hostschlüssel noch nicht bestätigt. Unter Instanzen per SSH verbinden und Fingerprint unabhängig mit Thunder vergleichen.");
        try
        {
            var outcome = await _ssh.ProbeAuthenticationAsync(_sshEndpoint, ct);
            return outcome.Success
                ? ("Erfolgreich getestet", "Authentifizierte SSH-Verbindung mit bestehendem Hostvertrauen erfolgreich. Nur ein harmloser true-Befehl ausgeführt.")
                : ("Fehler", "SSH-Anmeldung gescheitert. Pod-Port, SSH-Key-Zuordnung und hinterlegten Hostfingerprint prüfen.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ExternalCommandException)
        {
            return ("Fehler", "SSH-Verbindung fehlgeschlagen. Port, Hostschlüssel und Schlüsselzuordnung prüfen. Keine Hostprüfung abgeschaltet.");
        }
    }

    private async Task<(string, string)> CheckTokenApiAsync(string service,
        string url, CancellationToken ct)
    {
        var token = _readSecret(service);
        if (string.IsNullOrWhiteSpace(token))
            return ("Optional · fehlt", "Kein Token hinterlegt. Nur erforderlich, falls ausgewählte Modelle einen Token oder Gated-Model-Zugriff benötigen.");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("ThunderComputeClient/0.1");
        using var response = await _http.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, ct);
        return response.StatusCode switch {
            HttpStatusCode.OK => ("Erfolgreich getestet",
                "Token wurde vom Anbieter bei einer lesenden Konto-Abfrage akzeptiert. Einzelne Modellrechte nicht geprüft."),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                ("Fehler", "Token abgelehnt oder ohne passende Rechte. Im Anbieter-Konto prüfen bzw. erneuern."),
            HttpStatusCode.TooManyRequests => ("Nicht prüfbar",
                "Anbieter begrenzt die API-Abfragen; später erneut testen."),
            _ => ("Nicht prüfbar",
                "Anbieter antwortet mit HTTP " + (int)response.StatusCode +
                "; Token damit nicht als gültig bestätigt.")
        };
    }

    public void Dispose() { if (_disposeHttp) _http.Dispose(); }
}
