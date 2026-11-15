# Architektur

## Grenzen

- Presentation: App.xaml, MainWindow.xaml und MainWindow.xaml.cs (Dialog-/Drag-and-drop-Adapter).
- ViewModel: MainViewModel mit bindbaren Properties, ICommand-Aktionen und geschützten Bestätigungsdialogen.
- Core: GitWorkspace, WorkflowCatalog, ThunderCli, SshService, ProcessRunner, CredentialVault, SettingsStore.
- Extern: tnr CLI, Git for Windows mit GCM, OpenSSH, privates runpod-ai-workflows Repository und offizieller GitHub-Releases-Endpunkt.

Keine direkten undokumentierten Thunder-HTTP-Aufrufe. Instanzstatus und Snapshots werden über JSON-Ausgabe der offiziellen tnr-CLI gelesen. Für die tatsächliche Create-Verfügbarkeit ist ausschließlich der Create-Versuch relevant.

## Git/Manifeste

Workflow-Datenquelle: https://github.com/thomasasen/runpod-ai-workflows (nur lesend). Erster Checkout per git clone in temporäres Verzeichnis, dann atomare Verzeichnisverschiebung. Bei bestehenden Checkouts: Origin prüfen; nur Branch main; git status --porcelain muss leer sein; git fetch origin main; sicherer Fast-Forward nur wenn HEAD Vorgänger von FETCH_HEAD ist. Bei jeder Synchronisierungsstörung ist die Verwendung eines lokalen Standes ausdrücklich zu bestätigen.

Manifest-Schema: workflows/<Ordner>/model.json, Modelle im Feld models, vorhandene workflow*.json-Canvas-Dateien und README.md. Die Modell-Zielidentität wird durch Kategorie/Dateiname bestimmt. Konflikte (gleiches Ziel, unterschiedliche Quelle) werden sichtbar gemeldet und verhindern einen Bootstrap. Unbekannte Größen bleiben unbekannt; keine Schätzwerte für unbekannte Manifestgrößen.

## Thunder und Kosten

tnr create --json --gpu ... --num-gpus ... --vcpus ... --template ... --disk ... .
tnr status --json --no-wait für vor/nach dem Versuch; kein automatischer weiterer Versuch nach unklarem Fehler. Über ein exklusives Lockfile wird gleichzeitig nur ein Create aus dem selben Benutzerprofil ausgeführt. Wenn andere Instanzen existieren, ist automatische Erstellung gesperrt, statt eine irrtümliche Duplikatinstanz zu erzeugen.

GUI-Preise sind zunächst editierbare historische Listenpreis-Schätzwerte vom 15.11.2026; zusätzliche Disk geschätzt über 0,219 USD/GB/Monat und 730 Stunden. Der Client verfügt nicht über einen serverseitigen Budget-Cutoff, verifizierte Live-Preise oder zuverlässigen Guthabenabruf.

## SSH/Bootstrap

tnr connect <id> --json stellt offizielle SSH-Verbindungsparameter und Schlüssel bereit. OpenSSH verbindet mit IdentitiesOnly, BatchMode und StrictHostKeyChecking=yes und einer anwendungseigenen known_hosts-Datei. Neue ED25519-Hostschlüssel brauchen expliziten Fingerprint-Abgleich über unabhängigen vertrauenswürdigen Kanal.

Secrets aus Windows Credential Manager werden dem SSH-Prozess als Standard-Eingabestrom bereitgestellt. Der lokale Checkout liefert ausschließlich die vorhandene thunder/bootstrap.sh. Bash übernimmt Token und WORKFLOWS nur als Prozess-Umgebung; keine Secrets auf CLI-Argumentliste, in URLs, in einer Remote-Konfigurationsdatei oder in Git. Die Remote-Adapterlogik startet ihrerseits scripts/pod_entry.py und den bestehenden zentralen Bootstrap. ComfyUI bleibt auf 127.0.0.1:8188; lokaler SSH-Tunnel auf 127.0.0.1:8188.

## Qualität/Testgrenzen

ICommandRunner dient als externe Schnittstelle; automatisierte Tests ersetzen echte tnr/ssh/git-Prozesse durch Fakes. Kein echter kostenpflichtiger Create-, SSH- oder Modelldownload-Livetest. Codeabdeckung ist nicht mit vollständiger Runtime-Validierung gleichzusetzen. Fehlende Funktionen und Sicherheitsrisiken sind in IMPLEMENTATION_STATUS.md und SECURITY_REVIEW.md dokumentiert.

