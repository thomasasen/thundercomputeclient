# Implementierungsstand (15.11.2026)

Dies ist ein initialer, zusammenhängender Windows-Client-Codebestand, **keine als vollständig validiert deklarierte Endversion**.

| Bereich | Stand |
|---|---|
| .NET 10 WPF, deutsche GUI, MVVM, Dark/Light | Sidebar/Cards, Light/Dark; GPU-Zeilen und Ressourcenformular neu gerastert, Startvorschau strukturiert, primärer Kostenbutton dauerhaft sichtbar. Windows-CI rendert sieben Ansichten und prüft 1320/1080px, Endanwender-Sichttest noch offen |
| Privater GitHub-Katalog, API-Synchronisierung, Offline-Zustimmung | **Kein Checkout**: API-Abfrage mit Fine-grained PAT aus WinCred; Commit-/Blob-SHA, nur benötigte Dateien im RAM, Offline nur innerhalb laufender Sitzung. Echter PAT-Zugriff am Benutzer-PC noch nicht validiert |
| Dynamische Workflow-Manifeste, Canvas, Mehrfachauswahl, Modell-Deduplikation | Implementiert; komplexe Manifest-Metadaten/Hardwareempfehlungen nicht vollständig |
| GPU-Priorität, Editieren, Kostenbestätigung, sichere Fallback-Regel | Implementiert; Preise nur historische Schätzung, GPU/vCPU live von CLI validiert |
| Thunder Status, Create, Delete, Snapshot Create/List, Snapshot-Template | Implementiert mit dokumentierten tnr-JSON-Befehlen; nicht live getestet |
| Windows Credential Manager, GitHub-API-PAT / Thunder CLI Auth | Implementiert; Git GCM nicht mehr nötig. tnr.exe-Pfaderkennung, Windows-Fehlerdiagnose. Echte Kontoverbindungen nicht live getestet |
| Remote SSH Host-Fingerprint-Zustimmung, Bootstrap via Standard-Eingabe | Implementiert; lokaler Adapter aus Commit-gepinntem GitHub-Blob im Speicher. Remote-Entrypoint lädt weiterhin main; Resume nach Abbruch nicht automatisiert |
| Privater Loopback-Tunnel, ComfyUI im Browser, SSH-Terminal | Implementiert, End-to-End nicht live getestet |
| Download-/Installationslogs | Strukturierte pro-Modelldatei-Ereignisse aus vorhandenem download_monitor.py werden beobachtet; Fortschritt nur bei tatsächlicher Logangabe, keine erfundenen Werte. Persistente Reconnect-Historie fehlt |
| GPU-/CPU-/RAM-/Disk-Auslastung | Hardware-Textdiagnostik über SSH; Instanzstatus automatisch alle 30 Sekunden, noch keine kontinuierlichen Hardwaregrafiken |
| Kostenüberwachung | Lokale Schätzung, kein Guthaben-/Livepreisabruf, keine echte Kostensperre |
| Zusätzliche Bootstrap-Umgebungsvariablen, Konfigurationsprofile | Noch nicht umgesetzt |
| GitHub Release-Versionsprüfung | Implementiert; keine automatische Updateinstallation |
| GitHub Actions Windows-Build, Tests, Inno Installer, SHA-256 | Windows-CI mit privaten GitHub-API-Mocktests, tnr-Pfad-Regressionen und sieben GUI-Ansichten; Windows-Installer und SHA-256. Echte GitHub-API-Anmeldung am Benutzergerät noch nicht bestätigt |
| Unabhängiges Red-Team-Review / echter Windows-/GPU-Test | Noch ausstehend; Merge nicht vor abgeschlossener QS |

## Bewusste Sicherheitsgrenzen

Es werden keine undokumentierten Thunder-API-Endpunkte, Live-Verfügbarkeiten, tatsächlichen Preise, Snapshotkosten oder Downloadprozentsätze erfunden. Ein nicht eindeutiger CLI-Create-Fehler unterbindet jeden weiteren automatischen Create-Versuch. Andere vorhandene Instanzen blockieren konservativ den automatischen Create, auch wenn der Benutzer eine zweite Instanz wünschen könnte. Eine echte serverseitige Kostensperre ist nicht verfügbar.

## Für die nächste Ausbaustufe

- Dynamisches Thunder-GPU-Spezifikations-/Preis-Mapping über eine offiziell unterstützte Datenquelle und zusätzliche GPU-Familien.
- Vollständige dynamische Auswertung der Workflow-README/Manifeste inklusive VRAM-Empfehlungen.
- Download-/Custom-Node-Protokollparser mit echten Modelldatei-Statusereignissen und Remote-Resume nach Disconnect.
- Profile und fortgeschrittene Bootstrap-Umgebungseinstellungen.
- Belastbare lokale Historie / Reconnect-Statusautomaten und fortlaufendes Hardwaremonitoring.
- Durchgängige Windows-UI-Tests, statische Security-Tools und unabhängiger Sicherheitsreview vor einem Merge.

