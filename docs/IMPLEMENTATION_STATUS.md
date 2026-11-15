# Implementierungsstand (15.11.2026)

Dies ist ein initialer, zusammenhängender Windows-Client-Codebestand, **keine als vollständig validiert deklarierte Endversion**.

| Bereich | Stand |
|---|---|
| .NET 10 WPF, deutsche GUI, MVVM, Dark/Light | Implementiert; Automatisierter WPF-Start-/ContentRendered-Test in CI ergänzt, Endanwender-Smoketest weiterhin offen |
| Git-Checkout, Startup-Sync, Fast-Forward, Offline-Zustimmung | Implementiert; echte GCM-Anmeldung nicht live getestet |
| Dynamische Workflow-Manifeste, Canvas, Mehrfachauswahl, Modell-Deduplikation | Implementiert; komplexe Manifest-Metadaten/Hardwareempfehlungen nicht vollständig |
| GPU-Priorität, Editieren, Kostenbestätigung, sichere Fallback-Regel | Implementiert; Preise nur historische Schätzung, GPU/vCPU live von CLI validiert |
| Thunder Status, Create, Delete, Snapshot Create/List, Snapshot-Template | Implementiert mit dokumentierten tnr-JSON-Befehlen; nicht live getestet |
| Windows Credential Manager, Git GCM / Thunder CLI Auth | Implementiert, echte Kontoverbindungen nicht live getestet |
| Remote SSH Host-Fingerprint-Zustimmung, Bootstrap via Standard-Eingabe | Implementiert, Remote-Resumption nach Abbruch nicht automatisiert |
| Privater Loopback-Tunnel, ComfyUI im Browser, SSH-Terminal | Implementiert, End-to-End nicht live getestet |
| Download-/Installationslogs | Strukturierte pro-Modelldatei-Ereignisse aus vorhandenem download_monitor.py werden beobachtet; Fortschritt nur bei tatsächlicher Logangabe, keine erfundenen Werte. Persistente Reconnect-Historie fehlt |
| GPU-/CPU-/RAM-/Disk-Auslastung | Hardware-Textdiagnostik über SSH; Instanzstatus automatisch alle 30 Sekunden, noch keine kontinuierlichen Hardwaregrafiken |
| Kostenüberwachung | Lokale Schätzung, kein Guthaben-/Livepreisabruf, keine echte Kostensperre |
| Zusätzliche Bootstrap-Umgebungsvariablen, Konfigurationsprofile | Noch nicht umgesetzt |
| GitHub Release-Versionsprüfung | Implementiert; keine automatische Updateinstallation |
| GitHub Actions Windows-Build, Tests, Inno Installer, SHA-256 | Windows-CI inklusive WPF-GUI-Starttest, Installer und SHA-256; realer Benutzer-PC weiterhin ungetestet |
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

