# Thunder Compute Client v0.1.0

**Erstes regulär veröffentlichtes Windows-Release (win-x64).** Der Installer, sein SHA-256-Prüfsummenfile und die Release-Notizen werden aus demselben erfolgreichen GitHub-Actions-Lauf erstellt.

## Lieferumfang

- Nativer deutschsprachiger Desktop-Client für Windows 11, .NET 10 und WPF mit acht Bereichen, Workflow-Katalog aus dem privaten `thomasasen/runpod-ai-workflows` und Mehrfachauswahl.
- Thunder-CLI-Integration für Instanzen, sichere SSH-Anbindung und privaten ComfyUI-Tunnel (`127.0.0.1:8188`). SSH-Hostfingerprints müssen unabhängig verifiziert werden.
- Automatische Pod-Erstellung nur nach ausdrücklicher Kostenbestätigung; vorhandene Instanz wird bei eindeutiger Zuordnung nach Bestätigung wiederverwendet. Providerpreise und Spezifikationen werden vor kostenpflichtiger Neuerstellung abgefragt.
- Gepinnter Bootstrap auf die ausgewählte GitHub-Revision, Remote-Job mit Reattach und Log-Streaming, Downloadstatus sowie mehrstufige Bereitstellungsprüfung.
- GPU-VRAM-Preflight-Fix aus [PR #14](https://github.com/thomasasen/thundercomputeclient/pull/14): `nvidia-smi`-Werte in MiB werden korrekt in **dezimale GB** umgerechnet. Bisher scheiterte insbesondere die nominelle 48-GB-Klasse (RTX A6000/L40) an einem falschen Vergleich mit GiB. Fehlerberichte zeigen nun die tatsächlich ermittelte Kapazität und die geforderte Referenz.

## Validierung und bekannte Grenzen

- GitHub Actions: Windows Build, .NET-Mock-/Regressionstests, Python-Readiness-Fixtures, WPF-Smoke-Test (alle acht Ansichten), Inno-Setup-Installer, SHA-256, Linux-Test für Remote-Supervisor/Detach/Reattach.
- **Kein erfolgreich abgeschlossener Live-End-to-End-Test** von Thunder-Create bis zu vollständig installierten Modellen, Custom Nodes, ComfyUI und einer realen GPU-Inferenz. Tests beweisen diese Laufzeitfunktion nicht.
- **Nicht codesigniert.** Der Installer kann Windows-SmartScreen-Warnungen auslösen. SHA-256 gegen `SHA256SUMS.txt` prüfen.
- **Keine serverseitige Kostensperre.** GPU-Instanzen können nach einem Fehler weiter Geld kosten. Status unter **Instanzen** bzw. in der Thunder-Konsole prüfen; eine VM wird nicht automatisch gelöscht.
- Eine nominale 48-GB-GPU erfüllt die unterste README-Orientierung des Krea2-FaceHand-SeedVR2-Workflows, **garantiert aber keine erfolgreiche Inferenz**. SeedVR2 7B FP16 kann bei hohen Auflösungen Offloading oder mehr VRAM benötigen.
- Vollständige unabhängige Sicherheitsprüfung, signierter Installer und realer Mehrstunden-/Multi-Pod-Betriebstest sind weiter offen. Dieses Release ist für bewusste Evaluierung gedacht, **nicht als Nachweis einer produktionsreifen unbeaufsichtigten Bereitstellung**.

## Installation und Update

1. `ThunderClient-Setup-0.1.0-win-x64.exe` aus den Assets dieser GitHub-Release-Seite herunterladen.
2. Optional die Prüfsumme mit `Get-FileHash .\ThunderClient-Setup-0.1.0-win-x64.exe -Algorithm SHA256` kontrollieren und mit `SHA256SUMS.txt` vergleichen.
3. Bisherige Anwendung beenden, Installer ausführen. Der Installationspfad bleibt unter dem Benutzerprofil. Zugangsdaten weiterhin nur im Windows Credential Manager verwalten.
4. In **Zugangsdaten** den GitHub-Fine-grained-PAT (`Contents: Read-only` für das private Workflow-Repository) und erforderliche Thunder/HF/Civitai-Tokens hinterlegen. Unter **Systemstatus** prüfen.
5. **Vorhandenen Thunder-Pod zuerst unter Instanzen prüfen und wiederverwenden. Nicht zusätzlich einen Pod erstellen**, wenn bereits eine kostenpflichtige Instanz läuft.

Dokumentation: [README](https://github.com/thomasasen/thundercomputeclient/tree/main#readme), [Troubleshooting](https://github.com/thomasasen/thundercomputeclient/blob/main/docs/TROUBLESHOOTING.md).