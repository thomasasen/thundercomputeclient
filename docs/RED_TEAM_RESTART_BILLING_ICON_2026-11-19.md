# Red-Team-Review: Git-Neustart, Laufzeitkosten und Windows-Icon (19.11.2026)

## Abgrenzung / Release-Status

Diese Änderung wird als Review-Branch/PR geführt. **Keine Produktionsfreigabe** und kein kostenpflichtiger Thunder-Livetest. Die korrekte Installation auf dem echten Windows-PC und ein Live-Test des SSH-/Git-Upstreams sind weiter offen. Kein automatisches Merge bei fehlendem Review gemäß `docs/SECURITY_REVIEW.md`.

## Anbieter- und Quellprüfung

- Thunder CLI `tnr status --json --no-wait` liefert `cpuCores`, `gpuType`, `numGpus`, `storage` und die Instanzidentität; der Python-/Shell-Start wird über den weiterhin authentifizierten privaten GitHub-Contents-/Blob-Pfad ausgeführt.
- Thunder `GET /v2/pricing` und `GET /v2/specs` liefern aktuelle öffentliche Tarif-/Spezifikationswerte. Das ist **keine Rechnungs-/Guthaben-API**.
- Offizielle Thunder-Billing-Dokumentation: https://console.thundercompute.com/settings/billing und https://www.thundercompute.com/pricing. Die offizielle MCP-Schnittstelle nennt `get_meter_data`, `get_upcoming_invoice`, `get_invoice_history` und `get_subscription` mit separatem OAuth. Ein automatisch lesbarer *Balance*-Wert ist in diesen Dokumenten nicht als garantiertes Feld angegeben.
- Das Upstream-`scripts/bootstrap.py` verwendet eine Dateiherkunfts-/Fingerprint-Prüfung für bestehende Modelle; unveränderte Dateien werden übersprungen. Veränderte oder kaputte Modelle können neu geladen werden, ganz ohne neue Pod-Instanz.

## Negativ- und Funktionstests

| Angriffs-/Fehlerszenario | Absicherung |
|---|---|
| GitHub offline, ungültige Revision oder geänderte Workflow-Slugs | Preflight vor Stop; kein stilles Offline-Fallback |
| Mehrfachauswahl mit Modell-Zielkonflikt oder Freigabeflag | Neustart blockiert |
| Pod fehlt, UUID wechselt oder Status nicht RUNNING | Neustart blockiert |
| SSH-Hostfingerprint nicht geprüft | Neuer read-only SSH-Pfad scannt und bestätigt Hostfingerprint unabhängig; kein SSH-Login ohne Trust |
| Es ist noch kein SSH-Key für den Pod autorisiert | Restart blockiert statt `tnr connect` oder `/add_key` mit einem neuen Public Key aufzurufen |
| Remote-Job doppelt gestartet | Remote-`flock`, Run-ID und PID-Prüfung |
| Prozess zu PID gehört fremder Anwendung | Kein Signal und kein Metadatenüberschreiben |
| Unbekannte Anwendung belegt Port 8188 | Kein konkurrierender ComfyUI-Start |
| Eigene Prozessgruppe beendet nicht binnen 45 Sekunden | Kein `kill -9`; Neustart bricht ab |
| Vorhandenes Modell-/Outputverzeichnis | Remote-Supervisor entfernt keine Modelldateien |
| Token im Remote-Skript/Export | Worker bleibt credential-frei; Token via SSH-stdin/Environment, Logredaktion |
| Fehlende `cpuCores` oder unbekannte GPU | Kein errechneter Fantasiepreis |
| Unbekannte historische Laufzeit/Rabatte | Kein falscher Rechnungsbetrag oder automatisierter Guthabenwert |
| Installer-/Anwendungsicon | ICO als Git-Blob-Resource referenziert; Windows-CI erforderlich |

## Verbleibende Risiken

1. Ein tatsächlich laufender ComfyUI-Prozess kann beim Neustart aktive Inferenzjobs verlieren. Hierfür gibt es die manuelle Bestätigung.
2. Temporäre Downloadartefakte oder neue Manifestversionen können zusätzliche Datenübertragungen auslösen. Persistente Modell- und Outputverzeichnisse bleiben erhalten, aber die Upstream-Software selbst kontrolliert ihre Datenoperationen.
3. Thunders aktuelle öffentliche Preisliste ist nicht identisch mit dem tatsächlich bereits abgerechneten Betrag (Promotion, Rabatt, Snapshot, Tarifänderung). Der Balancewert ist nur in der Anbieter-Konsole verlässlich einsehbar, bis eine autorisierte, spezifizierte Billing-API integriert und getestet wurde.
4. Ein anderer Prozess desselben Linux-Benutzers kann Umgebungsvariablen lesen, wie im bestehenden Sicherheitsreview dokumentiert. Unabhängige Security-Abnahme ist weiter erforderlich.
5. Die Quellen für das Windows-Icon sind eine für die Icon-Verwendung optimierte grafische Umsetzung des bereitgestellten Blitzmotivs. Die im Repository gespeicherte ICO enthält kleine Windows-Größen für Titelzeile, Taskleiste und Installer; eine zusätzliche Pixel-für-Pixel-Originalprüfung ist ausstehend.
6. Ohne Anbieter-Pod werden realer Git-Download, SSH-Signalsicherung, ComfyUI-/CUDA-Start und persistente Speicherbelegung nicht end-to-end bestätigt.

## Abnahme

- Windows-CI: .NET-Tests, WPF-GUI-Smoketest, Publish und Inno-Setup-Build.
- Linux-CI: Shell-Syntax und echter Detached-Worker-Test mit Reattach/Restart, Modelldatei-Sentinel, fremdem HTTP-Listener und fremder PID.
- Vor Merge außerdem Installerinstallation/Deinstallation auf Windows, separater SSH-Test auf dem vorhandenen Pod und unabhängiger Review gemäß `docs/SECURITY_REVIEW.md`. **Bei diesen Punkten besteht ausdrücklich noch keine Abnahme.**
