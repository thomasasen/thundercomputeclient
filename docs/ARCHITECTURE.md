# Architektur

## Automatischer Ein-Klick-Deploy

`AutoProvisioning.Plan` berechnet aus dem tatsächlich ausgewählten und GitHub-Commit-identifizierten Workflow-Katalog die deduplizierte Modellgröße, den Diskbedarf (Modellgrößen + 40 GB pauschale Reserve bzw. die höhere RunPod-Referenz) und die Kandidaten aus der vom Benutzer aktivierten GPU-Reihenfolge. Kandidaten unter der dokumentierten VRAM-Orientierung oder über der benutzerseitig freigegebenen **historischen** Preisgrenze werden ausgeschlossen. Fehlende Modellgrößen, kollidierende Modelldateien sowie deklarierte `required_authorization_env`-Pflichten blockieren diese automatische Aktion. Eine fehlende Civitai-Anmeldung bei gewählten Civitai-Modellen blockiert ebenfalls den Vollinstallationsmodus. Keine Live-GPU-/Preis-/CUDA-Kompatibilität wird behauptet.

`MainViewModel.AutoDeployAsync` friert Plan, gewählte Workflows, Commit und Bootstrap-Skript vor dem ersten Anbieteraufruf ein. Die Kosten-/Wiederverwendungsbestätigung ist der letzte Schritt vor dem kostenpflichtigen `tnr create`. Existiert genau ein Pod, wird er mit erneuter Bestätigung wiederverwendet; mehrere Pods verhindern automatische Zuordnung. Die bisherige `ThunderCli.CreateWithFallbackAsync`-Sperre gegen Doppelstarts bleibt unverändert. `AutoProvisioning.WaitForRunningSshAsync` wartet begrenzt auf `RUNNING` mit IPv4/IPv6 und valide angegebenem Port. Bei Fehlern erfolgt **niemals ein weiterer Create-Versuch**.

`TrustedSshAsync` ruft die offizielle `tnr connect --json` zur Schlüsselvorbereitung auf, scannt ED25519 und fragt im unveränderten Fingerprint-Dialog nach unabhängiger Bestätigung. Danach wird eine echte SSH-Anmeldung mit zeitbegrenztem `true` getestet. Ein vorhandenes, bereits HTTP-antwortendes ComfyUI auf Pod-Loopback wird nicht ungefragt durch parallele Bootstrap-Prozesse gestört.

Anschließend übergibt `SshService.BootstrapAsync` den vorhandenen `thunder/bootstrap.sh` mit `WORKFLOWS` und `MODEL_DOWNLOAD_WORKERS=4` über SSH-stdin. Der Upstream startet `early_workers.py` (Modelle und Nodes parallel) sowie `download_scheduler.py` (providerabhängig adaptiv, höchstens vier Datei-Jobs) und geht schließlich per `exec` direkt in ComfyUI über. **Deshalb ist das Bootstrap-Task langfristig aktiv und nicht beim Start von ComfyUI abgeschlossen.** Der Client wertet `[DOWNLOAD-STATUS]`-Logereignisse und `phase=comfyui_http_ready status=ok` als Fortschritts-/Bereitschaftssignale aus; der echte Remote-Prozess bleibt über die SSH-Verbindung gebunden.

Kein automatischer Neustart nach Verbindungsverlust, kein unbedingtes Entfernen kostenpflichtiger Ressourcen und **noch kein persistenter Hintergrund-Supervisor**. Für diesen ersten Modus muss die GUI geöffnet bleiben; Abbruch und unklarer Remote-Zustand erfordern eine bewusste Prüfung. Der Remote-Adapter ruft weiter den Entrypoint von beweglichem `main` ab; die lokal angezeigte GitHub-Commit-SHA ist deshalb **kein vollständiger Remote-Deployment-Pin**.

## Lokale Voraussetzungen und Statusprüfungen

`SystemReadinessService` stellt die Einrichtungsprüfung bereit. `SetupCheck` hält pro Bereich Status, Zeitstempel, verständliche Erklärung, offizielle URL und GUI-Navigationsziel; `MainViewModel` startet Prüfungen nur auf ausdrücklichen Klick, ohne Secret-Text in Log oder Status zu spiegeln. `MainWindow.xaml` zeigt die achte Seite und navigiert über ausschließlich freigegebene HTTPS-Hosts zu offiziellen Hilfeseiten. Jede Prüfung ist unabhängig und ihre Ergebnisse sind klar von bloßer Dateiexistenz getrennt.

- GitHub: bestehender `GitHubWorkflowSource.FetchAsync` verifiziert über Bearer-Token die private Repo-Tree-/Blob-Revision, **kein anonymer öffentlicher Check**.
- Thunder: `tnr status --json --no-wait`, anschließend JSON-Validierung; erzeugt keinen Pod.
- OpenSSH: nur Dateiprüfung für `ssh.exe`, `ssh-keyscan.exe` und `ssh-keygen.exe` an Windows-Installations- oder PATH-Orten.
- SSH-Schlüssel: Laufender Thunder-Pod erforderlich; `tnr connect <id> --json` liefert den validierten Endpunkt/Schlüsselpfad; der Client kontrolliert nur, ob die private Schlüsseldatei existiert.
- SSH-Anmeldung: Vorhandener, früher durch den Benutzer bestätigter `known_hosts`-Eintrag ist Voraussetzung. Erst dann wird mit `StrictHostKeyChecking=yes` ein harmloser SSH-`true`-Befehl ausgeführt. Kein automatisches Akzeptieren von SSH-Fingerprints.
- HF: `GET https://huggingface.co/api/whoami-v2`; Civitai: `GET https://civitai.com/api/v1/me`. Nur HTTPS-Bearer-Header, keine Redirects oder Cookies, keine Antwortdaten im UI. Das beweist lediglich Konto-API-Anmeldung, nicht gated model permissions.

Tests verwenden injizierte Fake-Runner und HTTP-Handler ohne Live-Pod-/Provider-Zugangsdaten. Das Frontend hat acht Seiten im Windows-Smoke-Test; echte API-/SSH-Tests auf dem Benutzerrechner bleiben nötig.

## Grenzen

- **WPF-Oberfläche:** `App.xaml`, `MainWindow.xaml` und `MainWindow.xaml.cs`; sieben Seiten, Desktop-Dialoge und Drag-and-Drop.
- **MVVM:** `MainViewModel` mit ICommand, Zustands-/Fehlermeldungen, expliziten Kosten- und SSH-Fingerprint-Dialogen.
- **GitHub-Quelle:** `GitHubWorkflowSource` liest ausschließlich das **private** `thomasasen/runpod-ai-workflows` über authentifizierte GitHub-HTTPS-REST-API. `WorkflowCatalog` parst die geladenen Manifeste im Speicher.
- **Core:** `ThunderCli` mit offizieller `tnr`-CLI, `SshService`, `ProcessRunner`, `CredentialVault`, `SettingsStore`.
- **Extern:** `api.github.com`, offizielle Thunder-CLI, OpenSSH, privates Workflow-Repository und öffentliches GitHub-Release-API-Endpunkt.

## Commit-fixierte private GitHub-API

Ablauf: GET `/repos/thomasasen/runpod-ai-workflows/commits/main` -> HEAD-Commit und Tree-SHA -> GET `/git/trees/{sha}?recursive=1` -> Liste der verfügbaren Workflow-Manifeste, Canvas-Dateinamen, README-Objekte und des Bootstrap-Adapters -> GET `/git/blobs/{blobSha}` für die tatsächlich notwendigen kleinen Textdateien. Die Blob-SHA gehören zur **einen ausgewählten Commit-Revision**.

Ausschließlich `workflows/*/model.json`, `workflows/*/README.md` und `thunder/bootstrap.sh` werden inhaltlich geladen. `workflow*.json`-Dateinamen werden aus dem Tree entnommen, die Canvas-Dateien selbst werden nicht heruntergeladen. Ordner `incoming/` mit Windows-ungültigen Dateinamen haben deshalb keinen Einfluss mehr.

Authentifizierung: Fine-grained PAT mit **Contents: Read-only** für das eine private Repository aus dem Windows Credential Manager. Token wird pro API-Aufruf als `Authorization: Bearer` über HTTPS gesendet, nicht in URLs oder Logs gespeichert. HTTP-Weiterleitungen und Cookies sind deaktiviert, Dateigrößen und Baumvollständigkeit werden validiert, begrenzte parallele Blob-Requests werden eingesetzt. Die Inhalte bleiben nur im RAM, ohne Checkout oder lokalen Cache.

Bei einem Fehler wird die vorhandene Snapshot-Instanz **nicht** ausgetauscht. Offline-Nutzung ist nach expliziter Zustimmung nur für einen bereits in **derselben Sitzung** erfolgreich geladenen Stand möglich. Ein Start ohne Token kann die Katalogdaten nicht abrufen.

## Geführte Pod-Bereitstellung und Hardwareberatung

Das UI verbindet vier bestehende Schritte: Katalog laden (private GitHub-API), Workflow-Auswahl (Mehrfachauswahl mit Dateikonfliktprüfung), Hardwareplanung und Pod-Verwaltung. Die Seite Workflows speichert die Auswahl vor dem Wechsel zur Hardwareseite. Nach erfolgreichem bestätigtem Create-Command signalisiert das ViewModel das Ereignis `PodCreated`; das Fenster navigiert zur Verwaltung und hält die neu erstellte Instanz ausgewählt.

`HardwareRecommendations.Parse` liest ausschließlich den Abschnitt `## Persistent Volume und GPU-Empfehlungen` der privaten Workflow-README-Dateien. Angaben sind **RunPod-Referenzen** und keine Thunder-Spezifikationen oder gemessenen Werte. `HardwareRecommendations.Plan` erzeugt eine in der GUI deutlich gekennzeichnete VRAM-Orientierung und einen Disk-Planungswert aus deduplizierter Modellgröße + 40 GB Pauschalreserve sowie dokumentiertem RunPod-Persistent-Volume. Bei fehlenden Größen wird keine Diskempfehlung erfunden. Bei mehreren Workflows wird die strengere VRAM-Referenz herangezogen; das Laufzeitverhalten einer gemeinsamen Instanz ist nicht gemessen.

Die Anwendung ändert Einstellungen nie allein durch die Auswahl eines Workflows. Erst `ApplyHardwareGuidanceCommand` erlaubt ein Erhöhen der Disk und ein Filtern der lokalen GPU-Kandidaten anhand der **VRAM-Klasse**. vCPU, Template, Preisgrenze und GPU-Anzahl bleiben unter Benutzerkontrolle. Thunder-`tnr`-Create und konservative Fallback-Regeln sowie die separate Kostenbestätigung bleiben unverändert.

## Manifest- und Kostensicherheit

Manifest-Schema: `workflows/<Ordner>/model.json` mit Feld `models`, vorhandene `workflow*.json`-Canvas-Dateinamen und optional `README.md`. Dateiidentität = Kategorie/Dateiname. Doppelte Modellziele mit unterschiedlicher Quelle blockieren den Bootstrap. Fehlende Größen bleiben unbekannt.

Der Client verwendet ausschließlich offiziell dokumentierte Thunder-`tnr`-Befehle. Kostenschätzungen sind lokale, historische Schätzwerte. Ein Erstellungsversuch erfordert eine ausdrückliche Zustimmung. Nach uneindeutigen API-/CLI-Fehlern erfolgt **kein** automatischer weiterer Create-Versuch.

## SSH und Remote-Bootstrap

`tnr connect <id> --json` stellt SSH-Parameter. OpenSSH verwendet BatchMode, IdentitiesOnly, StrictHostKeyChecking=yes und eigene known_hosts-Datei. Der Benutzer muss neue Fingerprints über eine unabhängige Thunder-Quelle prüfen.

Der `thunder/bootstrap.sh`-Inhalt aus dem authentifizierten GitHub-Blob wird dem bestätigten SSH-Prozess über **stdin** übergeben. Der Lesetoken und optionale HF/Civitai-Token werden nicht in URL oder Prozessargumente geschrieben. Der Remote-Adapter startet weiterhin `scripts/pod_entry.py` aus `main` des privaten Repositories; **dieser Remote-Schritt ist noch nicht auf die lokal angezeigte Commit-SHA fixiert** und bleibt Freigaberisiko. ComfyUI bleibt nur über privaten Tunnel auf `127.0.0.1:8188` erreichbar.

## Tests / Grenzen

Mocktests prüfen GitHub-API-Autorisierung, Blob-/Commit-Konsistenz, Windows-ungültige Pfade und Negativfälle, den Workflow-Katalog, Thunder-Create-Fallback, Downloadstatus und den Windows-`tnr`-Suchpfad. Die Windows-CI startet die echte WPF-EXE, rendert sieben Ansichten und baut den Installer. Ein echter privater GitHub-API-Test mit auf dem Benutzer-PC gespeichertem Token sowie Live-Thunder-/SSH-Tests sind davon **nicht** abgedeckt.
