# Thunder Compute Client v0.1.6 – Pod-Software-Neustart, Live-Kosten und Blitz-Icon

## Neu

- **Pod-Software auf bestehender Instanz neu starten:** In „Instanzen“ und „Downloads & Logs“ kann die neue GitHub-`main`-Revision abgerufen, auf eine feste Commit-SHA geprüft und der vorhandene ComfyUI-/Bootstrap-Prozess nach ausdrücklicher Zustimmung kontrolliert neu gestartet werden. Die Aktion führt weder `tnr create` noch `tnr delete` oder einen Betriebssystem-Neustart aus.
- **Persistente Modelle und Outputs erhalten:** Der Neustart entfernt keine Modelldateien, Workspaces oder Outputs. Der vorhandene Upstream-Downloader erkennt unveränderte Modelldateien wieder. Neue, geänderte oder unvollständige Modelle können weiterhin Datenverkehr und Downloadzeit verursachen.
- **Kostenkarte für den ausgewählten laufenden Pod:** Der Client liest die tatsächliche GPU-/vCPU-/Disk-Konfiguration und ermittelt damit den aktuellen öffentlichen Thunder-Tarif (USD/h), aufgeschlüsselt nach Ressourcen. Die rechnerischen Laufzeitkosten werden ausdrücklich von tatsächlich abgerechneten Beträgen unterschieden.
- **Thunder-Balance:** Direkter Link zur authentifizierten Thunder-Abrechnungsseite. Weil die bisher verwendete Thunder-CLI und REST-Schnittstelle keinen verifizierten Guthabenwert liefern, wird keine erfundene Balance angezeigt.
- **Blitz-Icon:** Als Windows-Anwendungs-, Taskleisten-, Seitenleisten- und Installer-Icon eingebunden.

## Sicherheit

- Vor dem Neustart müssen frische Git-Revision, Workflows/Modell-Ziele, ausgewählte Instanz, verifizierter SSH-Zugang mit einem **bereits autorisierten Schlüssel** sowie die ausdrückliche Benutzerbestätigung vorliegen.
- Ein Neustart registriert keine neuen SSH-Schlüssel. Fremde Prozesse/PIDs, bereits belegter ComfyUI-Port 8188 und unbekannte Bootstrap-Sperren führen zum Abbruch.
- Der Remote-Supervisor beendet nur seine eigene verifizierte Prozessgruppe. Ein aktiver Download oder Inferenzjob kann durch den Neustart unterbrochen werden.

## Tests und Grenzen

Windows-CI prüft .NET-Build/Unit-Tests, GUI-Start, veröffentlichtes Win-x64-Binary und Inno-Setup-Installer. Linux-CI prüft den Remote-Supervisor einschließlich Restart-/Prozessschutz und Erhalt einer Modelldatei. **Es wurde kein kostenpflichtiger Thunder-Pod-Livetest durchgeführt.** Der echte SSH- und ComfyUI-Neustart sowie die Rechnungsanzeige müssen am vorhandenen Pod separat überprüft werden. Der Stundentarif ist keine historische Rechnung, kein Guthaben und keine Abrechnungsobergrenze.

## Installation

Datei `ThunderClient-Setup-0.1.6-win-x64.exe` aus diesem GitHub-Release herunterladen und die zugehörige `SHA256SUMS.txt` prüfen. Die vorhandene Installationskennung bleibt gleich; die ältere Version wird dadurch aktualisiert.
