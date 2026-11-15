# Thunder Compute Client v0.1.0-alpha.5

**Hotfix für den Git-Workflow-Checkout unter Windows. Weiterhin Alpha, keine Produktionsfreigabe.**

## Behoben

- Der bisherige `git clone` wollte das vollständige private Workflow-Repository auschecken. Unter `incoming/Krea 2 + FLUX.2: ...` liegt jedoch ein Verzeichnisname mit Doppelpunkt. Der Pfad ist auf Windows nicht erlaubt und Git brach mit `fatal: unable to checkout working tree` ab.
- Die App führt den Clone nun ohne Checkout durch, aktiviert anschließend einen **Cone-Mode Sparse Checkout** für `workflows/`, `thunder/`, `scripts/`, `docs/` und Root-Dateien und checkt dann `main` aus. Das unzulässige `incoming/`-Verzeichnis wird nicht lokal materialisiert. Dasselbe gilt für Folge-Synchronisierungen. Das private Quellrepository wird nicht verändert.
- Nach dem Sparse Checkout prüft der Client, ob `workflows/` und `thunder/bootstrap.sh` tatsächlich vorhanden sind, bevor er den Checkout übernimmt.
- Fehler beim Start von Git und `tnr` werden als verständliche Meldungen statt unspezifischer `Win32Exception` angezeigt. Thunder-Status- und Snapshot-Fehler erscheinen mit ihren bereinigten Ursachen im Log.
- Zwei zusätzliche Regressionstests prüfen die sichere Kommandoabfolge beim Sparse-Clone und den Ausschluss von `incoming/`.

## Auf dem Windows-Rechner prüfen

1. Laufenden Client schließen, diesen Installer über die vorige Alpha installieren und App starten.
2. Unter **Übersicht** den Git-Synchronisierungsstatus prüfen. Unter **Workflows** sollten nach erfolgreichem Login die Manifeste erscheinen.
3. Bei einem Thunder-CLI-Fehler in PowerShell `Get-Command tnr` ausführen. `tnr` ist eine getrennte Voraussetzung und wird **nicht** mit dem GUI-Installer mitinstalliert.
4. Meldungen über **Downloads & Logs → Log kopieren** teilen (vorher auf Zugangsdaten prüfen).

## Test und Grenzen

Windows-CI: 20 mockbasierte Regressionstests, sieben WPF-Ansichten im Starttest, win-x64-Installer und SHA-256. Die Sparse-Checkout-Befehlsfolge wurde außerdem mit einem lokalen Git-Testrepository und einem Windows-ungültigen `incoming/`-Pfad überprüft. Ein vollständiger Live-Checkout des privaten Repositories am Windows-Endgerät steht noch aus.

Der Installer ist nicht signiert. Sicherheitsreview, GPU-/SSH-/Modelldownload-End-to-End-Test sowie echte Kostenüberprüfung sind offen. `main` bleibt unverändert; [Release-Gates](https://github.com/thomasasen/thundercomputeclient/issues/2).
