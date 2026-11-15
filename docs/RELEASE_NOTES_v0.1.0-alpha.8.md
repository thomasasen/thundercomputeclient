# Thunder Compute Client v0.1.0-alpha.8

**GUI-Optimierung für Windows 11 x64. Alpha-Vorabversion, keine Produktionsfreigabe.**

## Was sich optisch verbessert hat

- **Neue Instanz:** Durchgängig neu aufgebauter, ruhiger Konfigurationsbildschirm mit drei klaren Bereichen: GPU-Priorität, Ressourcen, Startvorschau.
- **GPU-Liste:** Breitere, sauber ausgerichtete Eingaben für vCPUs und geschätzte Stundenpreise; eindeutige Auswahlzustände und konsistente Abstände. Die GPU-Reihenfolge lässt sich weiterhin per Drag-and-Drop oder „Nach oben/Nach unten“ ändern.
- **Ressourcen:** GPU-Anzahl, Gesamtdisk, Template/Snapshot und maximale Stundenschätzung in einem zweispaltigen, responsiven Formular.
- **Fixierter Hauptbutton:** „Instanz erstellen …“ bleibt beim Scrollen immer sichtbar. Die ausdrückliche kostenrelevante Bestätigung bleibt zwingend.
- **Lesbarkeit:** Größere Basisschrift, einheitliche Formularlabels und besserer Farbkontrast. Der Schriftfarbenfehler auf primären blauen Buttons wurde korrigiert.
- **Startvorschau:** Besser lesbares, kopierbares Feld in einer eigenen Karte mit deutlichem Hinweis auf nur geschätzte Preise.

## Unverändert

- Workflow-Katalog kommt ohne Git-Checkout über die authentifizierte private GitHub-REST-API mit Fine-grained PAT (Contents: Read-only) aus WinCred.
- Offizielle Thunder-`tnr`-CLI und sicherer privater SSH-Tunnel bleiben Voraussetzungen.
- Keine neue GPU- oder Thunder-Aktion wird automatisch ausgelöst.

## Verifikation

Windows-GitHub-Actions: .NET-10-WPF-Build, bestehende Mock- und Regressionstests, GUI-Start mit allen sieben Ansichten, Sichtbarkeits-/Größenprüfung des GPU-Formulars und Aktionsbuttons bei 1320 px und 1080 px Fensterbreite, Installer und SHA256SUMS.

**Noch offen:** Praktische optische Prüfung am Benutzerrechner (insbesondere Windows-DPI 125%/150%), unabhängiges Security-Review, Code-Signing und Thunder-/GPU-/SSH-End-to-End-Validierung. Die angezeigten Preise sind Schätzungen, keine serverseitigen Kostenlimits.
