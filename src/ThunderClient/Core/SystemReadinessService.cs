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
            Id="openssh", Title="Windows OpenSSH · Client-Werkzeuge",
            Guide="In Windows → Einstellungen → System → Optionale Features den OpenSSH-Client installieren. Benötigt werden ssh.exe, ssh-keyscan.exe und ssh-keygen.exe. Kein SSH-Server auf deinem PC erforderlich.",
            HelpUrl="https://learn.microsoft.com/de-de/windows-server/administration/openssh/openssh_install_firstuse",
            HelpLabel="Microsoft-Installationsanleitung", SetupTabIndex=3
        },
        new SetupCheck {
            Id="ssh-key", Title="SSH-Schlüssel · ausgewählter Pod",
            Guide="Zuerst einen laufenden Pod unter Instanzen auswählen. Der Client fragt über 'tnr connect <ID> --json' den Schlüsselpfad ab und prüft nur, ob die private Schlüsseldatei existiert. Niemals den privaten Schlüssel teilen.",
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
            ? selected : pods.FirstOrDefault(x => x.Status == "RUNNING");
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

    private async Task<(string, string)> CheckSshKeyAsync(CancellationToken ct)
    {
        if (_runningPod is null)
            return ("Nicht prüfbar", "Kein laufender Pod gefunden. Zuerst einen bestehenden Pod auswählen oder erstellen.");
        if (FindExecutable("ssh") is null)
            return ("Fehlt", "SSH-Client fehlt; Windows OpenSSH installieren.");
        // The official CLI returns validated endpoint fields and key path.
        // Never read or display private key material.
        var cli = new ThunderCli(_runner);
        try
        {
            _sshEndpoint = await cli.GetSshAsync(_runningPod, ct);
            return ("Vorhanden", "Private SSH-Schlüsseldatei am von Thunder angegebenen Pfad gefunden (Dateiinhalt nicht gelesen). Anmeldung noch nicht getestet.");
        }
        catch (FormatException)
        {
            return ("Fehlt", "Thunder liefert noch keinen vollständigen SSH-Endpunkt oder die lokale private Schlüsseldatei fehlt.");
        }
        catch (ExternalCommandException)
        {
            return ("Fehler", "Thunder konnte den SSH-Endpunkt nicht ermitteln. Pod-Status und CLI-Anmeldung prüfen.");
        }
    }

    private async Task<(string, string)> CheckSshAuthenticationAsync(CancellationToken ct)
    {
        if (_sshEndpoint is null)
            return ("Nicht prüfbar", "Erst wenn ein laufender Pod und der private Schlüssel verfügbar sind, lässt sich SSH testen.");
        if (!await _ssh.HasKnownHostAsync(_sshEndpoint, ct))
            return ("Einrichtung nötig", "Hostschlüssel noch nicht bestätigt. Unter Instanzen per SSH verbinden und Fingerprint unabhängig mit Thunder vergleichen.");
        try
        {
            var outcome = await _ssh.RunScriptAsync(_sshEndpoint, "true\n", ct: ct);
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
