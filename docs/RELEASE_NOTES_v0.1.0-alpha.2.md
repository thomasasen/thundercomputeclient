# Thunder Compute Client v0.1.0-alpha.2

**Fehlerbehebungs- und Diagnoseversion für Windows 11 x64. Alpha-Vorabversion, nicht produktionsfreigegeben.**

## Änderungen gegenüber alpha.1
- **Startabsturz behoben:** Die read-only-Eigenschaften `Hardware` und `Preview` waren in WPF-Textfeldern versehentlich per `TwoWay` gebunden. Beide Bindungen sind jetzt `OneWay`. Der Fehler wurde reproduziert und der anschließende Fensterstart in Windows-CI erfolgreich getestet.
- Robustere Initialisierung des WPF-Hauptfensters mit sichtbarer Fehlermeldung bei Startproblemen.
- Startdiagnose unter `%LOCALAPPDATA%\\ThunderClient\\logs\\startup.log` (reduzierte, lokal gespeicherte Fehlerdetails).
- Automatischer Windows-GUI-Smoke-Test im CI: die echte veröffentlichte EXE muss das WPF-Fenster erstellen und vollständig rendern, ohne Git- oder Thunder-Zugriff.
- Falls die Git-/Thunder-Initialisierung scheitert, bleibt die Oberfläche grundsätzlich geöffnet und zeigt den Fehler an.

## Installation

Installer und SHA-256-Prüfsumme aus den Release-Assets laden, Prüfsumme vergleichen und mit Benutzerrechten installieren. Voraussetzung: Windows 11 x64. Weitere Voraussetzungen und Abläufe: [Benutzerhandbuch](https://github.com/thomasasen/thundercomputeclient/blob/feature/windows-desktop-client/docs/USER_GUIDE.md).

## Sicherheit und bekannte Grenzen

Der Installer ist **nicht signiert**. Preise sind Schätzungen; bei Interaktionen mit Thunder können echte Kosten entstehen. Live-GPU-/SSH-/Download-Tests, unabhängige Security-Prüfung und manuelle Windows-Smoke-Tests fehlen weiterhin. **Keine Produktionsfreigabe.** Offene Punkte: [Release-Gate #2](https://github.com/thomasasen/thundercomputeclient/issues/2), [Implementierungsstand](https://github.com/thomasasen/thundercomputeclient/blob/feature/windows-desktop-client/docs/IMPLEMENTATION_STATUS.md).

Diese Version beseitigt nicht automatisch unbekannte gerätespezifische Fehler. Bei ausbleibender GUI bitte das lokale Startprotokoll nach Prüfung auf sensible Inhalte weitergeben.
