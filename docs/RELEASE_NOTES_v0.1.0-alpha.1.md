# Thunder Compute Client v0.1.0-alpha.1

**Alpha-Vorabversion für Windows 11 x64. Nicht produktionsfreigegeben.**

Der native Windows-Client bietet eine deutschsprachige WPF-Oberfläche, dynamische Workflow-Auswahl aus dem privaten Repository, GPU-Prioritäten und Kostenschätzungen mit ausdrücklicher Bestätigung, Instanz- und Snapshotverwaltung, einen privaten SSH-Tunnel zu ComfyUI sowie Installationslogs. Der Client verwendet die offizielle Thunder-CLI und speichert optionale Zugangsdaten im Windows Credential Manager.

## Installation

1. `ThunderClient-Setup-0.1.0-alpha.1-win-x64.exe` aus den Assets herunterladen.
2. Den SHA-256-Hash mit `SHA256SUMS.txt` vergleichen (Windows PowerShell: `Get-FileHash -Algorithm SHA256 .\ThunderClient-Setup-0.1.0-alpha.1-win-x64.exe`).
3. Die Voraussetzungen aus dem [Benutzerhandbuch](https://github.com/thomasasen/thundercomputeclient/blob/feature/windows-desktop-client/docs/USER_GUIDE.md) einrichten: offizielle `tnr`-CLI mit Login, Git for Windows/Git Credential Manager, OpenSSH und Leserechte auf `thomasasen/runpod-ai-workflows`.
4. SSH-Hostfingerprints vor dem ersten Verbindungsaufbau **unabhängig verifizieren**. Kostenpflichtige Instanzen werden nur nach Bestätigung erstellt.

## Getestet

- Windows GitHub Actions: Restore, Build/Publish für win-x64 und Inno-Setup-Installer.
- **16/16 automatisierte, mockbasierte Tests erfolgreich**.
- Bei der Entwicklung wurde **keine** kostenpflichtige Thunder-Instanz erzeugt und **kein** Modelldownload ausgeführt.

## Bekannte Grenzen und Risiken

- **Kein unabhängiges Red-Team-Review, kein Installer-Smoke-Test auf einem echten Windows-11-PC und keine Thunder-/SSH-/GPU-End-to-End-Validierung.**
- Installer ist **nicht codesigniert**. Die Hashdatei ist eine Integritätskontrolle, ersetzt jedoch keine signierte Herkunftsprüfung.
- Preise sind **historische Schätzungen**, keine verifizierten Live-Tarife oder serverseitigen Kostenlimits.
- Automatisches Bootstrap-Resume nach Verbindungsabbruch, vollständige Hardware-Livemetriken und Konfigurationsprofile sind noch offen.
- Der remote ausgeführte Bootstrap wird noch nicht durch einen fixierten Git-Commit mit SHA-Pinning abgesichert.
- Windows 10 ist nicht validiert.

**Keine Empfehlung für produktive oder unbeaufsichtigte Nutzung.** Vollständiger Stand: [Implementierungsstatus](https://github.com/thomasasen/thundercomputeclient/blob/feature/windows-desktop-client/docs/IMPLEMENTATION_STATUS.md), [Security-Checkliste](https://github.com/thomasasen/thundercomputeclient/issues/2) und [offener Draft-PR #1](https://github.com/thomasasen/thundercomputeclient/pull/1). Das Workflow-Repository bleibt unverändert.