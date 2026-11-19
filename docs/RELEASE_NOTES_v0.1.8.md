# Thunder Compute Client v0.1.8 – Dark-Mode-Kontrastkorrektur

## Behoben

- Schwarze oder schlecht lesbare Schrift auf dunklen WPF-Oberflächen: TextBlocks, Überschriften, Labels und Listeneinträge beziehen ihre Schriftfarben ausdrücklich aus der aktiven Theme-Palette.
- Dropdowns/ComboBox-Einträge: Text und Auswahl-/Hover-Hintergrund sind auch im Dark Mode konsistent lesbar.
- Primäre (blaue) und destruktive (rote) Buttons: Beschriftung und Material-Symbol übernehmen die passende Button-Schriftfarbe; der Löschen-Button verwendet einen dynamischen `OnDangerBrush` statt hartkodiertem Weiß.
- Tooltips erhalten zum Theme passende Schrift-, Hintergrund- und Randfarben.

## Unverändert

- Alle Funktionen aus v0.1.7, darunter lokale Google Material Icons, farbige Statusanzeigen, sortierbare Download-Spalten und der per Maus verschiebbare Trenner zwischen Downloads und Logs.
- Keine Änderung an Thunder-CLI, SSH-Fingerprint-Verifikation, Bootstrap, Modellen, Pod-Kosten, Abrechnung oder Account-Balance. Der echte Thunder-Guthabenbetrag ist weiterhin nicht als verifizierte Live-Abfrage integriert.

## Prüfungen und Grenzen

Die GitHub-Windows-CI führt .NET-Tests, eine veröffentlichte WPF-App mit GUI-Starttest aller acht Bereiche einschließlich Dark-Mode-Kontrastprüfung und den Inno-Setup-Installer-Build aus. Die Linux-CI prüft den Remote-Supervisor. Der Release-Job verifiziert die SHA-256-Prüfsumme und veröffentlicht exakt den geprüften Windows-Installer.

**Nicht durchgeführt:** kostenpflichtiger Thunder-/GPU-End-to-End-Test sowie ein manueller Sichttest auf dem Ziel-Windows-PC, einschließlich unterschiedlicher Windows-Kontrastmodi und DPI-Skalierung.

## Installation

Auf Windows x64 `ThunderClient-Setup-0.1.8-win-x64.exe` herunterladen und mit `SHA256SUMS.txt` prüfen. Die Inno-Setup-AppId bleibt gegenüber v0.1.7 unverändert, damit eine Aktualisierung derselben Installation möglich ist.
