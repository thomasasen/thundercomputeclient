# Thunder Compute Client v0.1.0-alpha.6

**Alpha-Hotfix für Thunder-CLI-Erkennung und Prozessdiagnose auf Windows 11 x64. Keine Produktionsfreigabe.**

## Thunder-CLI in Program Files (x86) erkennen

Auf dem Benutzerrechner wurde `tnr.exe` in `C:\\Program Files (x86)\\tnr\\tnr.exe` nachgewiesen, obwohl die GUI zuvor lediglich eine `Win32Exception` für Status und Snapshots protokollierte.

Die Desktop-Anwendung ermittelt jetzt den ausführbaren Pfad von `tnr.exe` aus der eigenen `PATH`-Umgebung und zusätzlich aus den üblichen Windows-Installationsorten unter `Program Files (x86)` und `Program Files`. Dadurch kann der Client eine zuvor gestartete, veraltete Desktop-Prozessumgebung besser abfangen.

Bei einem fehlgeschlagenen Prozessstart werden jetzt Windows-Fehlernummer, tatsächlicher EXE-Pfad und eine verständliche Bezeichnung ausgegeben, unter anderem für Datei nicht gefunden, Zugriffsverweigerung, ungültiges Programm und Rechteerhöhung. Der Client fordert **keine** erhöhten Rechte automatisch an.

## Enthaltene Änderungen aus alpha.5

Git-Sparse-Checkout verhindert den vollständigen Windows-Checkout des unzulässigen Verzeichnisnamens in `incoming/`. Das private Workflow-Repository bleibt unverändert.

## Prüfung

Windows-CI mit **23 Mock-/Regressionstests**, inklusive drei neuer Tests für den CLI-Pfadfinder, sowie Starttest mit allen sieben WPF-Bereichen. Keine kostenpflichtigen Thunder-Operationen.

## Für die Fehlerdiagnose

In PowerShell unabhängig von der GUI prüfen:

```powershell
Get-Command tnr
tnr status --json --no-wait
```

Wenn die CLI läuft, im Client unter **Downloads & Logs → Log kopieren** die neue konkrete Fehlermeldung prüfen. Zugangsdaten nicht teilen.

Unverändert offen: Signatur, manuelle Windows-/Thunder-/GPU-/SSH-End-to-End-Tests, unabhängiger Security-Review. [Release-Gates](https://github.com/thomasasen/thundercomputeclient/issues/2).
