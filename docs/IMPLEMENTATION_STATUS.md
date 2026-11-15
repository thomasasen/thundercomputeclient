# Implementierungsstand (15.11.2026)

Dies ist ein initialer, zusammenhängender Windows-Client-Codebestand, **keine als vollständig validiert deklarierte Endversion**.

| Bereich | Stand |
|---|---|
| .NET 10 WPF, deutsche GUI, MVVM, Dark/Light | Sidebar/Cards, Light/Dark; GPU-Zeilen und Ressourcenformular neu gerastert, Startvorschau strukturiert, primärer Kostenbutton dauerhaft sichtbar. Windows-CI rendert sieben Ansichten und prüft 1320/1080px, Endanwender-Sichttest noch offen |
| Privater GitHub-Katalog, API-Synchronisierung, Offline-Zustimmung | **Kein Checkout**: API-Abfrage mit Fine-grained PAT aus WinCred; Commit-/Blob-SHA, nur benötigte Dateien im RAM, Offline nur innerhalb laufender Sitzung. Echter PAT-Zugriff am Benutzer-PC noch nicht validiert |
| Dynamische Workflow-Manifeste, Canvas, Mehrfachauswahl, Modell-Deduplikation | Implementiert; geführte Auswahl plus RunPod-Hardwaretabellen/Volumen aus READMEs, VRAM-/Disk-Planung und optionale manuelle Übernahme. Keine bestätigten Thunder-Live-Empfehlungen |
| GPU-Priorität, Editieren, Kostenbestätigung, sichere Fallback-Regel | Implementiert; Preise nur historische Schätzung, GPU/vCPU live von CLI validiert |
| Thunder Status, Create, Delete, Snapshot Create/List, Snapshot-Template | Implementiert mit dokumentierten tnr-JSON-Befehlen; nicht live getestet |
| Windows Credential Manager, GitHub-API-PAT / Thunder CLI Auth | Implementiert; Git GCM nicht mehr nötig. tnr.exe-Pfaderkennung, Windows-Fehlerdiagnose. Echte Kontoverbindungen nicht live getestet |
| Remote SSH Host-Fingerprint-Zustimmung, Bootstrap via Standard-Eingabe | Implementiert; SSH-Hostkey-Scan mit begrenztem Retry, Windows-OpenSSH-Priorität und diagnostizierbarem Exitcode/Host/Port (kein Auto-Trust). Ab alpha.12: SSH-Port im Instanzraster, konkrete JSON-Fehlerdiagnose aus CLI stdout, serieller Key-Setup-Aufruf; Systemstatus prüft SSH-Keys ohne side-effect-haltiges tnr connect. Keine echte SSH-Erreichbarkeitsprüfung am Benutzer-Pod. Adapter Commit-gepinnt, Remote-Entrypoint weiterhin main; Resume nicht automatisiert |
| Privater Loopback-Tunnel, ComfyUI im Browser, SSH-Terminal | Implementiert, End-to-End nicht live getestet |
| Download-/Installationslogs | Strukturierte pro-Modelldatei-Ereignisse aus vorhandenem download_monitor.py werden beobachtet; Fortschritt nur bei tatsächlicher Logangabe, keine erfundenen Werte. Persistente Reconnect-Historie fehlt |
| GPU-/CPU-/RAM-/Disk-Auslastung | Hardware-Textdiagnostik über SSH; Instanzstatus automatisch alle 30 Sekunden, noch keine kontinuierlichen Hardwaregrafiken |
| Kostenüberwachung | Lokale Schätzung, kein Guthaben-/Livepreisabruf, keine echte Kostensperre |
| Zusätzliche Bootstrap-Umgebungsvariablen, Konfigurationsprofile | Noch nicht umgesetzt |
| GitHub Release-Versionsprüfung | Implementiert; keine automatische Updateinstallation |
| GitHub Actions Windows-Build, Tests, Inno Installer, SHA-256 | Windows-CI mit GitHub-API-Mocktests, Hardwareplanung, SSH-CLI-JSON-Fehler- und Readiness-Sicherheitstests, tnr-Pfaden, SSH-Hostkey-Scan-/Sicherheitsregressionen und acht GUI-Ansichten; Windows-Installer und SHA-256. Echte GitHub-API-Anmeldung am Benutzergerät noch nicht bestätigt |
| Systemstatus / Onboarding | Achtseitige WPF-GUI mit sieben Diagnosepunkten für privates GitHub, Thunder CLI, OpenSSH, SSH-Schlüssel/Hostvertrauen, HF/Civitai. Echte lesende Checks nur auf Klick, Status-/Zeitstempel, offizielle Setup-Links, keine Kostenaktionen. Realgeräte-/Token-Test noch ausstehend |
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



## Ein-Klick-Bereitstellung (Alpha 0.1.0-alpha.13)

- **Umgesetzt:** ausgewählte Workflowordner → manifestbasierte GPU-/Diskplanung → ausdrückliche Kostenfreigabe → genau ein neuer Pod oder bestätigte Wiederverwendung → begrenztes RUNNING/SSH-Port-Warten → SSH-Schlüsselerstellung durch die offizielle CLI und unabhängige Hostfingerprint-Bestätigung → bestehender Thunder-Bootstrap mit bis zu vier adaptiven parallelen Modelldownloads und parallel laufenden Nodes → Liveprotokoll und ComfyUI-HTTP-Bereitschaft.
- **Sicherheitsgrenzen:** keine stillschweigende Preisobergrenzenerhöhung, bei unklaren Create-/SSH-/Downloadfehlern kein weiterer Pod, keine Auto-Löschung, bei erforderlicher zusätzlicher Manifestautorisierung keine automatische Bereitstellung.
- **Noch offen:** belastbarer Live-End-to-End-Test mit bestehendem Nutzer-Pod, persistenter Remote-Supervisor/Resume bei App-Abbruch, gepinnter Remote-Entrypoint statt beweglichem `main`, Live-Hardware-/Preise, unabhängige Red-Team-Prüfung und codesignierter Installer. GUI während des ersten automatischen Bootstrap geöffnet lassen.
