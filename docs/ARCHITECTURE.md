# Architektur

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

## Manifest- und Kostensicherheit

Manifest-Schema: `workflows/<Ordner>/model.json` mit Feld `models`, vorhandene `workflow*.json`-Canvas-Dateinamen und optional `README.md`. Dateiidentität = Kategorie/Dateiname. Doppelte Modellziele mit unterschiedlicher Quelle blockieren den Bootstrap. Fehlende Größen bleiben unbekannt.

Der Client verwendet ausschließlich offiziell dokumentierte Thunder-`tnr`-Befehle. Kostenschätzungen sind lokale, historische Schätzwerte. Ein Erstellungsversuch erfordert eine ausdrückliche Zustimmung. Nach uneindeutigen API-/CLI-Fehlern erfolgt **kein** automatischer weiterer Create-Versuch.

## SSH und Remote-Bootstrap

`tnr connect <id> --json` stellt SSH-Parameter. OpenSSH verwendet BatchMode, IdentitiesOnly, StrictHostKeyChecking=yes und eigene known_hosts-Datei. Der Benutzer muss neue Fingerprints über eine unabhängige Thunder-Quelle prüfen.

Der `thunder/bootstrap.sh`-Inhalt aus dem authentifizierten GitHub-Blob wird dem bestätigten SSH-Prozess über **stdin** übergeben. Der Lesetoken und optionale HF/Civitai-Token werden nicht in URL oder Prozessargumente geschrieben. Der Remote-Adapter startet weiterhin `scripts/pod_entry.py` aus `main` des privaten Repositories; **dieser Remote-Schritt ist noch nicht auf die lokal angezeigte Commit-SHA fixiert** und bleibt Freigaberisiko. ComfyUI bleibt nur über privaten Tunnel auf `127.0.0.1:8188` erreichbar.

## Tests / Grenzen

Mocktests prüfen GitHub-API-Autorisierung, Blob-/Commit-Konsistenz, Windows-ungültige Pfade und Negativfälle, den Workflow-Katalog, Thunder-Create-Fallback, Downloadstatus und den Windows-`tnr`-Suchpfad. Die Windows-CI startet die echte WPF-EXE, rendert sieben Ansichten und baut den Installer. Ein echter privater GitHub-API-Test mit auf dem Benutzer-PC gespeichertem Token sowie Live-Thunder-/SSH-Tests sind davon **nicht** abgedeckt.
