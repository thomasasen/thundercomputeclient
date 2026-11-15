# Thunder Compute Client

Nativer Windows-Desktop-Client (.NET 10/WPF) für ComfyUI-Workflows aus dem **privaten**, unveränderten GitHub-Repository `thomasasen/runpod-ai-workflows`.

**Status: Alpha-Vorabversion v0.1.0-alpha.8, keine Produktionsfreigabe.** Die Windows-CI prüft Kompilierung, Mock-/Regressionstests, den tatsächlichen WPF-Fensterstart mit allen sieben Bereichen und den Inno-Setup-Installer. Kein echter Thunder-/GPU-/SSH-/Modelldownload-End-to-End-Test.

## Privater GitHub-Zugriff: kein Checkout

Der Client verwendet für das Workflow-Repository **ausschließlich die authentifizierte GitHub-REST-API über HTTPS**, keinen `git clone`, keinen Sparse Checkout und keinen lokalen Arbeitsbaum. Beim Start sowie über „Katalog aktualisieren“:

1. Einen **Fine-grained Personal Access Token** im Windows Credential Manager lesen. Der Token muss ausschließlich für `thomasasen/runpod-ai-workflows` die Berechtigung **Contents: Read-only** haben.
2. Den `main`-Commit und den zugehörigen Git-Tree über die GitHub-API laden. Einträge unter `incoming/` werden nur als Metadaten empfangen und **nie als Dateien auf Windows ausgecheckt**. Die bekannten Windows-ungültigen Dateinamen sind deshalb unkritisch.
3. Ausschließlich `workflows/*/model.json`, gegebenenfalls die zugehörigen `README.md` und `thunder/bootstrap.sh` über ihre **Blob-SHA** abrufen; die Namen der `workflow*.json`-Dateien stehen im Tree. Workflows und Bootstrap liegen **nur im Arbeitsspeicher**, nicht als Cache auf der Festplatte.
4. Ein unvollständig/ungültig geladener Katalog wird nicht übernommen. Bei Netzwerkausfall kann nur der zuletzt in **derselben Sitzung** geladene Stand nach ausdrücklicher Zustimmung weiterverwendet werden.

Der Token wird nur in HTTPS-Authorization-Headern benutzt, nie als URL-Abfrageparameter, Git-Prozessargument oder Konfigurationsdatei. Die Anwendung lässt für GitHub-API-Requests keine Weiterleitungen zu anderen Hosts zu. Ein PAT ist **nicht** in der App enthalten und muss lokal vom berechtigten Benutzer hinterlegt werden. Die GitHub-Repository-Berechtigungen werden nicht geändert. Die Ausführung des vorhandenen Remote-Bootstraps über SSH braucht denselben Lesetoken.

## Voraussetzungen

- Windows 11 x64 (Windows 10 nicht validiert).
- Offizielle Thunder-Compute-CLI `tnr` mit `tnr login`. Die GUI findet `tnr.exe` über PATH bzw. die Standardverzeichnisse unter Program Files.
- OpenSSH-Client, `ssh-keygen` und `ssh-keyscan`.
- GitHub-Fine-grained-PAT mit **Contents: Read-only** für das private Workflow-Repository, im Tab **Zugangsdaten** in WinCred hinterlegt. Optional HF-/Civitai-Token.
- Vertrauenswürdige, externe Quelle zum Abgleich des SSH-ED25519-Hostfingerprints vor dem ersten Verbindungsaufbau.

**Git for Windows/Git Credential Manager sind für den GUI-Workflow-Katalog nicht mehr erforderlich.** Für andere Projekte können sie natürlich weiterhin installiert bleiben.

## Optimierte Instanzkonfiguration

Unter **Neue Instanz** sind GPU-Priorität, Ressourcen und Startvorschau jetzt in klar getrennten Karten angeordnet. Alle GPU-Zeilen besitzen ein einheitliches Raster mit ausreichend breiten Feldern für vCPUs und geschätzte GPU-Stundenpreise. Ressourcenfelder nutzen ein zweispaltiges Formular mit vollständigen Beschriftungen.

Die kostenpflichtige **Instanz erstellen**-Aktion bleibt beim Scrollen am unteren Rand sichtbar; die separate Sicherheitsbestätigung vor dem Erstellen bleibt unverändert. Der helle und dunkle Modus verwenden die korrekte Schriftfarbe auf primären Aktionsbuttons. Die Windows-GUI-Selbstprüfung kontrolliert beide Fensterbreiten (Standard und unterstütztes Minimum), alle sieben Seiten und den sichtbaren Erstellen-Button.

## Bedienung

1. App starten und unter **Zugangsdaten** den privaten GitHub-Lesetoken speichern, dann **Katalog aktualisieren**. Ohne Token bleibt die GUI offen und zeigt eine aussagekräftige Fehlermeldung.
2. Unter **Workflows** Einträge wählen. Einmalig geladene Metadaten werden in der laufenden Sitzung gehalten; eine neue Sitzung lädt die Daten erneut.
3. Unter **Neue Instanz** GPU-Reihenfolge, vCPUs, Disk, Template und geschätzte Stundengrenze prüfen. Die erste kostenpflichtige Erstellung braucht immer eine ausdrückliche Bestätigung.
4. Unter **Instanzen** Status/Snapshots abrufen, SSH-Fingerprint unabhängig verifizieren und dann über **Downloads & Logs** den Bootstrap starten.
5. ComfyUI über den **privaten SSH-Loopback-Tunnel** öffnen; vor dem Löschen fertiggestellte Sicherungen prüfen.

In **Downloads & Logs** ist das Protokoll schreibgeschützt, markierbar und mit Strg+C kopierbar. „Log kopieren“ und „Startdiagnose kopieren“ verwenden die Windows-Zwischenablage; private Informationen vor dem Teilen prüfen.

## Lokaler Build

```powershell
dotnet restore tests/ThunderClient.Tests/ThunderClient.Tests.csproj
dotnet test tests/ThunderClient.Tests/ThunderClient.Tests.csproj -c Release
dotnet publish src/ThunderClient/ThunderClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish
```

Der Windows-Installer wird in GitHub Actions erstellt; er ist noch nicht codesigniert. Die App installiert Releases nicht automatisch. **Preise sind unverifizierte Schätzungen und keine serverseitigen Kostenlimits.** Der Benutzer ist selbst für die Kontrolle kostenpflichtiger Ressourcen verantwortlich.

Siehe [Benutzerhandbuch](docs/USER_GUIDE.md), [Architektur](docs/ARCHITECTURE.md), [Sicherheitsgrenzen](docs/SECURITY_REVIEW.md), [Fehlerdiagnose](docs/TROUBLESHOOTING.md) und [Implementierungsstand](docs/IMPLEMENTATION_STATUS.md).
