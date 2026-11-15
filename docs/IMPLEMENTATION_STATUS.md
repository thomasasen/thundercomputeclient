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


## Alpha.14: Windows `ssh-keyscan` mit nicht unterstützter sntrup-KEX

Ein konkreter Windows-OpenSSH-Bug blockierte beim Nutzer `ssh-keyscan` am bereits `RUNNING` gestarteten Pod **vor** den Modell-Downloads. Der Client verwendet bei dieser spezifischen Diagnose jetzt einen nicht authentifizierten, in temporärer known_hosts-Datei isolierten `ssh.exe`-Handshake mit ED25519 und anschließender gesonderter Nutzer-Fingerprint-Freigabe. Das ist ein auf den bekannten Fehler begrenzter Fallback, **kein** Ausschalten von StrictHostKeyChecking im eigentlichen SSH-Zugriff. Der Dialog zeigt künftig nur Handlungsanweisungen; vollständige technische Details bleiben im kopierbaren Log. Tests simulieren die reale Fehlermeldung und prüfen den vergänglichen Hostkey-Speicher, das Fehlen von Credentials/Remote-Commands und Fail-Closed-Verhalten. Echter Pod-End-to-End-Test bleibt ausstehend; Red-Team-Findings RT-01–10 bleiben offen.


## Alpha.15: Interne SSH-Zeitlimits und Schrittprotokoll

- Beobachtet: Reale Nutzerinstallation von L40-Pod `0` bis SSH-Vorbereitung erfolgreich, danach generisches „A task was canceled“. Kein Bootstrap/Download gestartet. Der damalige Logeintrag erlaubt **keine exakte Zuordnung** zu einem bestimmten SSH-Teilprozess.
- Korrektur: `ProcessRunner` übersetzt **interne** Command-Zeitlimits in `TimeoutException` mit Programmname/Dauer und lässt **explizite** Cancellation separat. Die SSH-Vorbereitung protokolliert Thunder-Endpunkt, Hostvertrauen, Keyscan-Versuch, isolierte SSH-Key-Erfassung, Fingerprintfreigabe und Anmeldetest. Im Fallback kann ein bereits empfangener ED25519-Public-Key nach dem `ssh -N`-Zeitlimit geprüft werden. Unerwartete `OperationCanceledException` ohne abgebrochenen Deploy-Token werden als internes Zeitlimit mit letztem Schritt ausgegeben.
- Windows-CI-Regression: echtes zeitbegrenztes Windows-Kommando und explizite Benutzer-Cancellation sowie isolierter Hostkey-Fallback nach simuliertem Prozessdeadline. **Kein** echter SSH-Test auf einem Thunder-Pod in CI.
- Red-Team-P0 (nicht verifizierte Gesamtkosten / kein persistenter Remote-Supervisor) und andere P1 bleiben **offen**. Keine Stable-Freigabe.


## Alpha.16: Unklarer Thunder-API-`add_key`-Timeout

Die tatsächliche Nutzersitzung von 23:35 Uhr zeigte nach erfolgreicher L40-Pod-Erstellung: `tnr connect 0 --json` → `POST /v1/instances/0/add_key` → `context deadline exceeded` nach ca. 30 Sekunden. Die offizielle Thunder-CLI setzt `http.Client.Timeout = 30s`. **Dies ist ein Anbieter-API-Aufruf und nicht die SSH-/ComfyUI-Portfreigabe.** Weil POST auch nach Client-Timeout serverseitig wirksam geworden sein könnte, darf der Client `add_key` nicht ohne Zustimmung blind wiederholen. Nun spezifische Exception/Diagnose, human-readable Abbruch mit Kostenhinweis, explizites Wiederverwenden der VM nach Zustimmung (mit Hinweis auf mögliche zusätzliche Schlüsselregistrierung), offizielle Status-/Konsolenlinks in der GUI und Regressionstests. **Keine behauptete Behebung des serverseitigen Timeouts.** Echte Thunder-API-/SSH-/Download-E2E-Prüfung weiterhin ausstehend. Red-Team-P0/P1 bleiben offen.
