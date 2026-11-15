# Thunder Compute Client v0.1.0-alpha.7

**Windows-Hotfix: privater GitHub-Katalog ohne Git-Checkout. Alpha, keine Produktionsfreigabe.**

## Was sich ändert

- **Kein `git clone`, kein `sparse-checkout`, kein lokales Git-Arbeitsverzeichnis.** Der Client liest das private Workflow-Repository `thomasasen/runpod-ai-workflows` direkt über die GitHub-REST-API mit **Fine-grained PAT und Contents: Read-only**. Der PAT liegt im Windows Credential Manager; er wird nicht in URLs oder Konfigurationsdateien gespeichert.
- Katalog und `thunder/bootstrap.sh` werden auf eine bestimmte `main`-Commit-/Tree-/Blob-Revision fixiert und **nur in den Arbeitsspeicher** geladen. Ungültige Windows-Dateinamen unter `incoming/` können keinen Checkout mehr verhindern.
- Der Client lädt ausschließlich Model-Manifeste, optionale Workflow-READMEs und den bestehenden Thunder-Bootstrap als Inhalt. Die Canvas-Dateinamen kommen aus der Tree-Liste. Bei leerem oder unvollständigem Katalog wird nichts ersetzt.
- Unter **Zugangsdaten** denselben begrenzten GitHub-Lesetoken für Katalog und Remote-Bootstrap speichern. **Git for Windows ist nicht mehr erforderlich.**
- Offline-Nutzung nur eines bereits in derselben App-Sitzung erfolgreich geladenen Stands nach ausdrücklicher Bestätigung.
- Für das private Repository gelten weiterhin keine Änderungen oder Schreibzugriffe.

## Installation

Den Windows-Installer über die vorherige Alpha installieren, Anwendung starten, unter **Zugangsdaten** den PAT hinterlegen und **Katalog aktualisieren**. Beim Start wird der Abruf ebenfalls versucht. Bei 401/403/404 die Tokenberechtigung auf das private Repository überprüfen.

## Test / Grenzen

Windows-CI mit Mock-Tests für Token-Header, fixierte Blob-SHAs, Authentifizierungsfehler, abgeschnittene Bäume und Windows-ungültige Repo-Pfade. Die GUI und Installer-Pipeline werden weiterhin auf Windows geprüft. Ein Live-API-Test mit dem PAT auf dem Benutzerrechner ist **nicht** erfolgt.

**Sicherheitsgrenze:** Der lokale Adapter ist an einen konkreten Commit gebunden; sein auf Thunder laufender Entrypoint wird vom Upstream-Adapter weiterhin über `main` geladen. Ein vollständig commit-fixierter Remote-Deploy bleibt offen. Kostenlimits sind nur Schätzungen, Installer nicht signiert, Live-GPU-/SSH- und unabhängige Sicherheitstests ausstehend.
