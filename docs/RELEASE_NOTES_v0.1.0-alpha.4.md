# Thunder Compute Client v0.1.0-alpha.4

**Windows-11-x64-Alpha. Keine Produktionsfreigabe.**

## Kopierbare Protokolle

- Der Bereich **Downloads & Logs** zeigt das laufende Protokoll als markierbaren, schreibgeschützten Text statt einer nicht kopierbaren Liste. Einzelne Abschnitte lassen sich mit Maus/Strg+C, das ganze Feld mit Strg+A/Strg+C kopieren.
- **Log kopieren** kopiert alle im Programm vorhandenen, bereinigten Sitzungslogzeilen in die Windows-Zwischenablage.
- **Startdiagnose kopieren** liest `%LOCALAPPDATA%\ThunderClient\logs\startup.log` und kopiert den bereinigten Text.
- Ein Feedback erscheint unten im Fenster. Bei fehlendem Log oder belegter Zwischenablage wird keine falsche Erfolgsmeldung angezeigt.

**Hinweis:** Automatische Token-Maskierung ist nicht vollständig. Bitte vor der Weitergabe auf personenbezogene Pfade, Token und andere Geheimnisse prüfen.

## Verifikation

Windows-Runner: 18 Unit-/Regressionstests und automatischer WPF-Starttest, der alle sieben Ansichten sowie die kopierbare Log-Ansicht prüft. Keine kostenpflichtigen Instanzen oder Downloads.

## Noch offen

Nicht signierter Installer; Windows-Endanwender-/GPU-/SSH-End-to-End-Tests und unabhängige Sicherheitsprüfung fehlen. Preise sind Schätzungen, keine echte Kostensperre. [Freigabepunkte](https://github.com/thomasasen/thundercomputeclient/issues/2).
