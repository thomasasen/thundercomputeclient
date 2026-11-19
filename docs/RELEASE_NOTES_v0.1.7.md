# Thunder Compute Client v0.1.7 – Material-Icons und bessere Downloads-Bedienung

## Neu und verbessert

- **Google Material Icons** für Aktionen und Statusanzeigen in der Windows-Oberfläche. Die Symbole sind als lokale WPF-Vektorgeometrien eingebunden und funktionieren offline; die zugehörige Apache-2.0-Lizenz liegt im Repository. Button-Beschriftungen bleiben erhalten.
- **Downloads & Logs: alle drei Spalten sortierbar.** Ein Klick auf die Überschrift sortiert aufsteigend, der nächste absteigend. Modelldatei und Status werden nach Text sortiert, Transferfortschritt nach dem tatsächlich protokollierten numerischen Prozentwert. Unbekannte oder ungültige Fortschrittswerte werden nicht geschätzt.
- **Tabelle mit der Maus vergrößern oder verkleinern.** Ein horizontaler Trenner zwischen Downloadtabelle und Logfenster lässt sich vertikal ziehen. Spaltenbreiten sind ebenfalls per Maus änderbar. Beide Bereiche behalten eigene Scrollbars und Mindesthöhen.
- **Farbige Status-Badges mit Symbol und Text.** Erfolgreiche Downloadzustände werden grün, laufende blau, Wiederholungen gelb, Fehler rot und Wartestatus neutral dargestellt; auch Pod- und Systemstatus verwenden passende Statusfarben. Der Hell- und Dunkelmodus bleibt unterstützt.

## Unverändert

- Pod-Erstellung, SSH-Vertrauensprüfung, Neustart-/Bootstrap-Logik, Modell-Downloader und Abrechnung sind gegenüber v0.1.6 funktional unverändert.
- Der neue Installer aktualisiert die vorherige Installation über dieselbe Inno-Setup-AppId. Es findet kein automatischer Eingriff in einen Thunder-Pod statt.

## Tests und Grenzen

Die Windows-Pipeline kompiliert die .NET-10-WPF-Anwendung, führt die Unit- und Regressionstests sowie den Starttest aller acht GUI-Bereiche aus und baut den x64-Inno-Setup-Installer. Der Linux-Integrationslauf testet den Remote-Bootstrap-Supervisor offline. Der Release-Job prüft den Installer und die SHA-256-Prüfsumme, bevor er veröffentlicht.

**Nicht durchgeführt:** kostenpflichtige Thunder-/GPU-End-to-End-Tests oder ein manueller Maustest des verschiebbaren Trenners auf einem echten Windows-Desktop. Die UI-Zustände geben ausschließlich die tatsächlichen Daten der jeweiligen Quelle wieder.

## Installation

`ThunderClient-Setup-0.1.7-win-x64.exe` herunterladen und die mitgelieferte `SHA256SUMS.txt` prüfen. Auf Windows x64 starten.

Details: [Oberflächenbedienung und Icons](UI_MATERIAL_ICONS_AND_DOWNLOADS.md).
