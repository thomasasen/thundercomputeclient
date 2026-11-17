# Red-Team-Prüfbericht · Technische Funktion, Quellenkonformität und Usability

**Prüfdatum:** 15.11.2026 · **Zielstand:** `v0.1.0-alpha.13`, Commit `36bb661eafd66751d49b83f894566ed4c92d09c5`  
**Ergebnis:** **NICHT FREIGEGEBEN für Stable-/Produktivbetrieb**. Alpha nur mit ausdrücklicher Kenntnis der Risiken.  
**Prüfart:** adversariales statisches Architektur-/Code-/Quellenreview und heuristische UX-Prüfung; GitHub-CI-/Mock-Ergebnisse kontrolliert. **Kein unabhängiger Drittprüfer, kein tatsächlicher Penetrationstest, kein Live-SSH-/GPU-/Modelldownload, keine Nutzertests.** Diese Einschränkung darf nicht als bestandenes unabhängiges Red-Team-Gate ausgegeben werden.

## 1. Grundlagen, Nachweise und Grenzen

- Originaler Entwicklungsauftrag „Entwicklungsauftrag: Thunder Compute Desktop Client“ (vom Auftraggeber übermittelte Spezifikation, Kapitel 1–15), insbesondere dynamische private Workflow-Quelle, keine unnötigen GPU-Kosten, Recovery, brauchbare GUI und explizite Kostenbestätigung.
- Aktueller Client-Code [Feature-Branch](https://github.com/thomasasen/thundercomputeclient/tree/feature/windows-desktop-client), [Draft-PR #1](https://github.com/thomasasen/thundercomputeclient/pull/1).
- **Verbindliche private Daten-/Bootstrap-Quelle:** `thomasasen/runpod-ai-workflows` (read-only geprüft): `workflows/*/model.json`, `README.md`, `workflow*.json`, `thunder/bootstrap.sh`, `scripts/pod_entry.py`, `start.sh`, `scripts/bootstrap.py`, `scripts/early_workers.py`, `scripts/download_scheduler.py`, `scripts/download_monitor.py`, `scripts/readiness_probe.py`. Im geprüften `main` sind **18 nutzbare Workflow-Ordner** mit 637 aktiven Modelleinträgen vorhanden (diese sind workflowübergreifend nicht einzigartig); `_template` ist keine nutzbare Auswahl. Alle 18 vorhandenen READMEs enthalten die GPU-/Volume-Referenzsektion. Der Client ruft ausschließlich private GitHub-Objekte mit Lesetoken ab und verändert die Quelle nicht.
- **Offizielle CLI-Quelle:** [Thunder-Compute/thunder-cli](https://github.com/Thunder-Compute/thunder-cli), besonders [`cmd/create.go`](https://github.com/Thunder-Compute/thunder-cli/blob/main/cmd/create.go), [`cmd/connect.go`](https://github.com/Thunder-Compute/thunder-cli/blob/main/cmd/connect.go), [`cmd/status.go`](https://github.com/Thunder-Compute/thunder-cli/blob/main/cmd/status.go), [`utils/spec_store.go`](https://github.com/Thunder-Compute/thunder-cli/blob/main/utils/spec_store.go), [`utils/pricing.go`](https://github.com/Thunder-Compute/thunder-cli/blob/main/utils/pricing.go), [`utils/keys.go`](https://github.com/Thunder-Compute/thunder-cli/blob/main/utils/keys.go), [`utils/thunderdir.go`](https://github.com/Thunder-Compute/thunder-cli/blob/main/utils/thunderdir.go).
- Providerbelege: [Thunder GPU-Preise](https://www.thundercompute.com/pricing), [Thunder SSH](https://github.com/Thunder-Compute/thunder-compute-documentation/blob/main/guides/ssh-on-thunder-compute.mdx), [Civitai API-Authentisierung](https://github.com/civitai/civitai-developer-docs/blob/main/site/guide/authentication.md).
- **CI:** [Run #43750505654](https://github.com/thomasasen/thundercomputeclient/actions/runs/43750505654) erfolgreich; **63/63 .NET-Mock-/Regressionstests**, WPF-Fensterstart mit acht Views und Installerbau. Die [CI-Datei](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/.github/workflows/windows-ci.yml#L22) startet weder die echten Thunder-APIs noch einen Pod/SSH-Login; der UI-Smoke-Test prüft nur Seitenaufbau, Navigation und Mindestbreiten. **63/63 heißt nicht End-to-End funktionsfähig.**
- Keine Benutzer-Zugangsdaten wurden gelesen oder versendet; keine kostenpflichtigen Instanzen/Downloads ausgelöst. Die Usability-Beurteilung ist eine **Heuristik auf Basis realer WPF-Bindings, Dialoge und früherer Nutzer-Screenshots**, kein beobachteter Endnutzer-Test.

## 2. Positiv verifiziert

**Korrekte Quellen und Verfahren:** Der private GitHub-Tree und Blob-Dateien werden über die authentifizierte API von einer konkreten Commit-Revision gelesen, ohne Windows-Checkout-Probleme mit unzulässigen Dateinamen ([src/ThunderClient/Core/GitHubWorkflowSource.cs:39](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/GitHubWorkflowSource.cs#L39)). Die 18 gültigen Workflows werden aus Manifest + README entdeckt, gemeinsam verwendete Modellziele dedupliziert ([src/ThunderClient/Core/WorkflowCatalog.cs:1](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/WorkflowCatalog.cs#L1)). Für die konkrete aktuelle Workflow-Quelle wurden keine widersprüchlichen Modell-Target-Quellen gefunden. Der Client nutzt den **bestehenden** privaten Thunder-Adapter und nicht einen zweiten Downloadimplementierungszweig. Im Upstream erfolgen frühe Model-/Node-Worker parallel; `MODEL_DOWNLOAD_WORKERS=4` verwendet den begrenzten adaptiven Scheduler. Die offizielle CLI unterstützt `tnr status --json --no-wait`, `tnr create --json`, `tnr connect <id> --json`, `tnr ports list --json`; Instanz-ID `0` ist regulär. Civitai `GET /api/v1/me` mit Bearer-Header entspricht der offiziellen Anbieter-Dokumentation.

**Sicherheitsvorkehrungen:** Windows Credential Manager; GitHub-PAT mit nur Leserechten; SSH `StrictHostKeyChecking=yes` mit manuellem Fingerprint-Dialog; kein öffentlicher ComfyUI-Port für den vorgesehenen SSH-Tunnel; kein automatisches erneutes Pod-Create bei unklarem Fehler; explizite Kostenzustimmung; paralleler Create-Sperrfile; vorherige Workflows-Kollisionserkennung; kein automatisches Git-Write auf die private Quelle.

Diese Kontrollen sind gute Bausteine. Sie schließen die folgenden Freigabemängel nicht aus.

## 3. Technische und sicherheitsrelevante Findings

### RT-01 · P0 / Releaseblocker · Lokale Kostengrenze lässt zu hohe Konfigurationen zu

**Nachweis:** [src/ThunderClient/Core/Models.cs:76](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/Models.cs#L76), [src/ThunderClient/Core/AutoProvisioning.cs:60](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/AutoProvisioning.cs#L60), [src/ThunderClient/ViewModels/MainViewModel.cs:633](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L633). `Estimates.Hourly` addiert nur GPU und Disk. **Zusätzliche vCPUs fehlen**, obwohl jede GPU-Zeile die vCPU-Anzahl editieren lässt. Zusätzlich lässt sich der geschätzte GPU-Stundensatz im Formular selbst editieren, ohne unabhängige Quelle. Die gesamte Auswahl wird ausschließlich gegen diese veränderbaren, unvollständigen Schätzungen geprüft.

**Reproduktion ohne Cloud-Kosten:** L40 (6 vCPUs inklusive), 12 vCPUs, 160 GB Disk, Budget 0,85 USD/h. Der Client rechnet ungefähr **0,808 USD/h** (`0,79+60*0,219/730`) und lässt die Kombination zu. Thunder nennt zusätzlich **0,04 USD pro weiterer vCPU/h**: Referenzwert etwa **1,048 USD/h** (`0,79+6*0,04+60*0,0003`), also über Budget. Dies ist eine Rechnung auf Grundlage der öffentlichen Preise, **kein Live-Rechnungsbeleg**.

**Fix:** Anbieter-Specs und Preiskomponenten aus der dokumentierten Schnittstelle prüfen; vCPU-Mehrkosten einrechnen; Maximalpreis der **teuersten zulässigen Fallback-Konfiguration** darstellen; bei unbekanntem Preis automatische Erstellung blockieren oder ausschließlich nach transparenter Zusatzfreigabe; GPU-Listenpreise nicht als frei editierbare „Wahrheit“ behandeln. Kostenregression mit Beispiel oben verpflichtend.

### RT-02 · P0 / Releaseblocker · Keine dauerhafte Ausführung und keine sichere Wiederaufnahme

**Nachweis:** [src/ThunderClient/Core/SshService.cs:170](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/SshService.cs#L170), [src/ThunderClient/Core/SshService.cs:138](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/SshService.cs#L138), [src/ThunderClient/ViewModels/MainViewModel.cs:633](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L633), [src/ThunderClient/ViewModels/MainViewModel.cs:590](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L590). Der Bootstrap läuft als SSH-`bash -s` aus dem GUI-Prozess, anschließend `exec` in einen langlebigen ComfyUI-Prozess. Es fehlt ein vom GUI-Lebenszyklus unabhängiger, überprüfbarer Remote-Service/Job mit stabiler ID, Statusdatei und Log-Reattach. Schließt der Benutzer die App oder bricht SSH ab, ist weder ein sicherer Fortschrittszustand noch ein gefahrloser Fortsetzungsweg implementiert. Ein Pod kann weiter kostenpflichtig laufen.

**Fix:** Remote-Job mit eindeutigem Deployment-ID, gekoppelter Manifest-/Commit-Revision, `systemd`-User-Service oder vergleichbarem Supervisor, 0600-Status-/Logdateien; wiederholbare, idempotente Download-/Node-Schritte; nach Restart ausschließlich **„Vorhandene Installation prüfen / Fortsetzen“**, kein blindes Restart/Create; eindeutige Trennung „Download abgeschlossen“, „ComfyUI antwortet“ und „für alle gewählten Workflows vollständig bereit“.

### RT-03 · P1 / Reproduzierbarkeit · Geplanter Katalogstand ≠ remote ausgeführter Stand

**Nachweis:** [src/ThunderClient/Core/GitHubWorkflowSource.cs:39](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/GitHubWorkflowSource.cs#L39), [src/ThunderClient/Core/SshService.cs:188](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/SshService.cs#L188) und Upstream-`thunder/bootstrap.sh` mit `REPO_REF=main`, das seinen Entrypoint von `main` herunterlädt und anschließend einen späteren Snapshot auflöst. Der Kosten-Dialog nennt eine konkrete Commit-SHA, aber der Pod kann nach einem Zwischenupdate anderen Bootstrap-/Manifestcode laden. Das widerspricht einem überprüfbaren Deployment-Snapshot.

**Fix:** Remote Bootstrap, `pod_entry.py` und Archive durchgängig an dieselbe zuvor geprüfte unveränderliche SHA binden (Upstream-Änderung nur nach expliziter technischer Abstimmung), Hash/Revision im Remote-Status nachweisen, Abbruch bei Abweichung.

### RT-04 · P1 / falsche Hardware · Vorhandenen Pod ohne echte Kompatibilitätsprüfung wiederverwenden

**Nachweis:** [src/ThunderClient/ViewModels/MainViewModel.cs:667](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L667), [src/ThunderClient/ViewModels/MainViewModel.cs:676](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L676). Genau ein vorhandener Pod wird als Ziel verwendet; überprüft werden Status und bei `StorageGb>0` der nominelle Datenträger. GPU-Modell, **VRAM**, CUDA-/Treiberstand, tatsächlich freier Speicher, vorhandene Installation und ausgewählte Workflows werden davor nicht geprüft. Bei `StorageGb=0` entfällt auch die Disk-Sperre.

**Fix:** Read-only Remote-Prüfung vor Bootstrap (`nvidia-smi`, `df`, Betriebssystem/Torch/CUDA, GPU/VRAM, Installationsmarker), bei unbekannten Daten nicht „passend“ anzeigen; mit nachvollziehbarer Abweichung und konkreten Auswahlmöglichkeiten stoppen.

### RT-05 · P1 / falscher Erfolg · ComfyUI-HTTP-Antwort ist kein vollständiger Workflowstatus

**Nachweis:** [src/ThunderClient/ViewModels/MainViewModel.cs:758](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L758), [src/ThunderClient/Core/SshService.cs:154](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/SshService.cs#L154). Ein bestehender Dienst auf `127.0.0.1:8188/system_stats` mit HTTP 200 führt dazu, dass die gesamte Installation übersprungen wird; die Manifest-Revision, tatsächlich vorhandene Modelle, Custom Nodes, fehlgeschlagene Downloads und der ComfyUI-Prozess werden nicht verifiziert. Der Readiness-Log gibt „HTTP bereit“ aus, was nicht `WORKFLOW READY` ist.

**Fix:** Separater Status `COMFYUI_HTTP_READY` und `WORKFLOW_READY`; Workflow-Revision, Modell-/Node-Liste und `download_failures.json` gegen die aktuelle Auswahl prüfen. Bei Partialität den Nutzer zu „Fehlende Modelle fortsetzen“ führen, nicht automatisch als fertig markieren.

### RT-06 · P1 / vermeidbare Kosten · Kein vollständiger Vorab-Systemcheck vor Kostenfreigabe

**Nachweis:** [src/ThunderClient/ViewModels/MainViewModel.cs:633](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L633) vs. [src/ThunderClient/Core/SystemReadinessService.cs:46](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/SystemReadinessService.cs#L46). Die optionale Systemstatus-Seite wird nicht als verpflichtende Vorstufe des automatischen Starts ausgeführt. Insbesondere wird ein vorhandenes Civitai-Token auf „nicht leer“ geprüft, **nicht auf Gültigkeit**; die SSH-Werkzeuge und die Thunder-Authentisierung werden nicht explizit als grüne Preflight-Gates angezeigt. Die konkrete SSH-Schlüsselregistrierung kann erst nach dem Pod-Start verifiziert werden; gerade deshalb ist eine klare Kosten-/Fehlerkommunikation erforderlich.

**Fix:** Alle **möglichen** lesenden Checks vor Create durchführen; fehlende/abgelaufene Token bzw. fehlendes OpenSSH blockieren; pro ausgewähltem Workflow Download-/Lizenzbedingungen offenlegen. Nach einem nicht vorab prüfbaren SSH-Fehler Pod-ID, Kostenzustand und sichere nächste Aktion anzeigen.

### RT-07 · P1 / Thunder-Vertrag · Statische GPU-Specs und Preisangaben statt Providerdaten

**Nachweis:** [src/ThunderClient/ViewModels/MainViewModel.cs:106](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L106), [src/ThunderClient/Core/AutoProvisioning.cs:23](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/AutoProvisioning.cs#L23), [src/ThunderClient/Core/Models.cs:76](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/Models.cs#L76). Der offizielle Thunder-CLI-Code verwendet dynamische `GetSpecs`, `GetAvailability`, `FetchPricing` sowie pro GPU×Anzahl gültige vCPU-/Storage-Bereiche; der Client bietet jedoch feste drei GPUs, frei editierbare vCPUs und global 50–2000 GB Disk an. So können nicht unterstützte Kombinationen erst **nach** der Kostenbestätigung scheitern. Ein manueller Preiswert kann fälschlich eine lokale Limitprüfung bestehen.

**Fix:** Read-only Specs/Pricing/Availability über dokumentierte CLI-Funktionen oder einen stabilen, offiziell unterstützten Adapter beziehen; Eingaben mit pro-Konfiguration-Grenzen/Validierungsfehlern versehen. Fehlen aktuelle Daten, keine „geprüfte Hardware“ oder echte Kostensicherheit suggerieren.

### RT-08 · P1 / unbeaufsichtigter Betrieb · Fehlende Recovery-Telemetrie und Kostenschutz

**Nachweis:** [src/ThunderClient/ViewModels/MainViewModel.cs:590](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L590), [src/ThunderClient/ViewModels/MainViewModel.cs:306](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L306), [src/ThunderClient/ViewModels/MainViewModel.cs:914](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L914). `Automatik abbrechen` unterbricht die SSH-Verbindung, **löscht den Pod bewusst nicht**; danach gibt es keinen geführten Stopp-/Snapshot-Pfad. Kosten hängen von historischer Schätzung/Startzeit ab, ohne garantierte Ausgabengrenze oder Guthaben. „ComfyUI öffnen“ prüft nur, ob ein Tunnelprozess nach kurzer Zeit noch läuft, nicht ob der Remote-HTTP-Endpunkt tatsächlich antwortet.

**Fix:** Ständige gut sichtbare „Pod läuft, kostet möglicherweise Geld“-Anzeige; nach Fehler CTA „Pod prüfen“, „Snapshot sichern“, „Pod löschen“ (mit separater Bestätigung); laufende Kosten aus belastbaren Anbieterwerten soweit möglich; vor Browseröffnung echten Loopback-HTTP-Check.

### RT-09 · P2 / Offline- und Quellenlücke · Nur In-Memory-Katalog, kaum Status der Canvas-Dateien

**Nachweis:** [src/ThunderClient/Core/GitHubWorkflowSource.cs:39](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/Core/GitHubWorkflowSource.cs#L39), [src/ThunderClient/ViewModels/MainViewModel.cs:403](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L403). Der direkte private API-Zugriff löst das bestätigte Windows-Git-Checkout-Problem sauber. Aber nach Programmneustart offline existiert kein persistenter, signiert bzw. auf SHA geprüfter letzter Katalog. `workflow*.json` wird auf **Dateinamen**, nicht den tatsächlichen Canvas-Inhalt validiert. Bei `hf_snapshot` berücksichtigt die Modell-Source-ID im Client nicht Repo/Revision/Dateiliste. Heute ist nur ein Snapshot-Modell betroffen, aber für zukünftige Kombinationen besteht Kollisionsrisiko.

**Fix:** SHA-verifizierter lokaler Cache ohne Windows-Checkout samt bewusster Offline-Freigabe; Canvas-Syntax-/Schema-Prüfungen nach Bedarf; korrekte Identity für HF-Snapshots.

### RT-10 · P2 / Sicherheitsreview · Keine externe Penetrations- und End-to-End-Prüfung

Die 63 automatisierten Tests nutzen Fakes; die echte Windows-UI wird nur gestartet und durchgeklickt. Es gibt **keinen** echten `tnr connect --json`-Erfolg, keine ComfyUI-/CUDA-Installation, keinen echten 80-Modell-Download, keinen Abbruch-/Resume-Test und keinen Screenreader-/Tastatur-Nutzertest. Der Installer ist nicht codesigniert. Der bisherige Autor kann die im Ursprungsauftrag geforderte **unabhängige** Red-Team-Freigabe nicht selbst ersetzen.

**Fix:** Separater Review durch nicht beteiligte Person/Team mit reproduzierbarem Prüflog und sign-off, plus explizit autorisierte Live-Tests außerhalb der kostenfreien CI.

## 4. Usability-Review für nichttechnische Nutzer

**Heuristisches Ergebnis: nicht ausreichend geführt.** Der Client wirkt deutlich aufgeräumter als die erste WPF-Version, setzt aber weiterhin erhebliches CLI-/Cloudwissen voraus.

| UX-ID | Beobachtung / Wirkung | Konkrete Verbesserung |
|---|---|---|
| UX-01 P1 | Zwei ähnlich wirkende Pfade: „Automatisch bereitstellen“, „Nur Pod erstellen“, „Bootstrap manuell starten“ sowie zusätzliche Pod-Schaltflächen. Ein Anfänger erkennt den Standardpfad nicht zuverlässig. | Ein dominanter Assistent: **Workflow auswählen → Vorschlag prüfen → Kosten bestätigen → Bereitstellung verfolgen → ComfyUI öffnen**. Fachoptionen in „Erweitert“. |
| UX-02 P1 | Technische Kürzel wie `vCPU`, `REPO_REF`, `SHA`, `Snapshot`, `VRAM`, `Bootstrap`, `TNR_HOME`, `WORKFLOWS` erscheinen ohne kontextabhängige Erklärung. | Klartext in der Primärebene („Arbeitsspeicher der GPU“, „Speicherplatz“, „Installation“); technische Details aufklappbar. |
| UX-03 P1 | `MainViewModel.Error` zeigt generische `MessageBox` mit Exceptiontext, [src/ThunderClient/ViewModels/MainViewModel.cs:1043](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/ViewModels/MainViewModel.cs#L1043). Fehlermeldungen sagen selten **was passiert ist, ob der Pod weiter kostet, was als Nächstes zu klicken ist**. | Strukturierte Fehlerkarten mit Bereich, Ursache, Kostenstatus, „Erneut prüfen“, „Zugangsdaten öffnen“, „Pod anzeigen“, „Diagnose kopieren“. Keine mehrfache Popup-Kaskade. |
| UX-04 P1 | `tnr login` verlangt weiterhin Windows Terminal und einen eigenen PowerShell-Befehl, [src/ThunderClient/MainWindow.xaml:575](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/MainWindow.xaml#L575). | „Thunder verbinden“ startet offiziellen CLI-Login kontrolliert, begleitet Rückkehr in die GUI und testet die Sitzung. |
| UX-05 P1 | Erstes SSH-Hostfingerprint muss laut [src/ThunderClient/MainWindow.xaml.cs:103](https://github.com/thomasasen/thundercomputeclient/blob/36bb661eafd66751d49b83f894566ed4c92d09c5/src/ThunderClient/MainWindow.xaml.cs#L103) über eine unabhängige Quelle geprüft werden. Die offizielle Thunder-SSH-Dokumentation zeigt den **manuellen SSH-Befehl**, aber keine eindeutig dokumentierte Anzeige des Server-Fingerprints für Anfänger. | Dokumentierten unabhängigen Vertrauensweg mit überprüfbarer Quelle anbieten oder den Nutzer ausdrücklich auf einen noch ungeklärten Vertrauensnachweis hinweisen; **nicht** `StrictHostKeyChecking` deaktivieren. |
| UX-06 P1 | „Bereit“ kann HTTP-Erreichbarkeit bedeuten, obwohl mehrere Modelle fehlen; heruntergeladene Dateien sind mit ihrem technischen Dateinamen im Raster statt mit verständlichen Fortschrittsgruppen dargestellt. | Separate Abschlussprüfung „ComfyUI läuft“, „Modelle X/Y“, „Nodes X/Y“, „ausgewählte Workflows einsatzbereit“, Fehler inkl. Retry-Hilfe. |
| UX-07 P2 | Preis-/vCPU-/Diskwerte sind normale Textfelder ohne klar sichtbares Validierungsfeedback. Ein ungültiger lokalisierter Zahlenwert kann im WPF-Binding scheitern, ohne einen eindeutigen Eintragsfehler anzuzeigen. Das ist **statisch als Risiko**, nicht durch manuelle Eingabe reproduziert. | Zahlenfelder mit Eingabebegrenzung, Deutsch/Englisch-Zahlenformat, unmittelbarer Inline-Validierung; vor Kostenfreigabe Werte nochmals als neue unveränderliche Zusammenfassung anzeigen. |
| UX-08 P2 | Die Workflow-Tabelle ist sehr technisch; keine kurze Zielbeschreibung, Beispielausgabe, „empfohlen für...“, Schwierigkeit oder verständliche Vorabwarnung. Zugänglichkeits-/Tastaturpfade wurden nicht praktisch geprüft. | Workflow-Karten mit Such-/Filterfunktion, Modellgröße, Speicherempfehlung, Token-/Lizenzbedarf; Tastatur-/Screenreader- und DPI-Tests auf realem Windows. |
| UX-09 P2 | Ein Abbruch zeigt überwiegend einen technischen Zustand; ein laiengerechter Folgeschritt fehlt. | Nach Abbruch persistente „Pod läuft möglicherweise kostenpflichtig“-Karte mit drei sicheren Entscheidungen: Status prüfen, Snapshot sichern, Löschen separat bestätigen. |

### Empfohlene UX-Fehlertexte und Aktionen

**SSH-Schlüssel nach kostenpflichtigem Pod-Start gescheitert:**  
„Dein Pod **#0 läuft**, die automatische Verbindung ist noch nicht gelungen. **Die Instanz kann weiterhin Kosten verursachen.** Es wurde **kein zweiter Pod** erstellt. [Verbindung erneut prüfen] [Thunder-Anmeldung öffnen] [Pod und Kosten ansehen] [Diagnose kopieren].“  
Der Detailbereich zeigt den konkret redigierten CLI-`error`-Code, nicht die komplette `Usage`-Ausgabe.

**Nicht ausreichende vorhandene Hardware:**  
„Der vorhandene Pod hat **48 GB GPU-Speicher**, für die ausgewählten Workflows ist **80 GB** als Orientierungswert dokumentiert. Wir starten keine Installation, die voraussichtlich scheitert. [Andere GPU auswählen] [Auswahl ändern] [Details anzeigen].“

**ComfyUI läuft, aber Modelle fehlen:**  
„ComfyUI antwortet, aber **4 von 80 Modelldateien fehlen oder sind fehlerhaft**. Diese Workflows sind noch nicht vollständig bereit. [Fehlende Downloads prüfen] [Zugangsdaten prüfen] [Protokoll kopieren].“  
Erst nach überprüftem vollständigem Modell-/Node-Audit `WORKFLOW_READY` zeigen; keine erfundene Erfolgsquote.

## 5. Explizite Abnahmekriterien vor stabilem Release

1. **Kosten:** Extra-vCPU-Kalkulation, GPU×Anzahl-Spec, Disk-Grenzen und dynamische Anbieterpreise korrekt bzw. bei fehlenden Daten ausdrücklich blockiert; Regression `L40, 12 vCPU, 160GB, 0,85 USD/h` wird zuverlässig nicht ungeprüft gestartet.
2. **Identität der Quelle:** Manifest, Bootstrap, pod_entry und Archiv auf **dieselbe konkrete GitHub-SHA** bezogen; Nachweis im Remote-Deploymentstatus.
3. **Lifecycle:** Nach Client-Schließen und Netzwerkabbruch wird ein laufender Remote-Job erkannt; Wiederaufnahme ohne neuen Pod und ohne doppelte, schädliche Downloads; Statusdatei und Logs bleiben nachvollziehbar.
4. **Bereits existierende Pods:** GPU/VRAM, freie Disk, Laufzeit, vorhandene Modelle und ComfyUI-Status vor Wiederverwendung lesend prüfen.
5. **Echte Bereitschaft:** 80/80 Modelle, 0 offene Pflicht-Fehler, benötigte Nodes/Canvases vorhanden, ComfyUI HTTP bereit. Fehlerbild mit Teilabschluss statt grüner Gesamterfolg.
6. **Anmeldung:** GitHub-/Thunder-/SSH-/HF-/Civitai-Voraussetzungen mit aussagekräftigen Abhilfen ohne manuelle PowerShell-Pflicht wo vermeidbar; keine SSH-Fingerprint-Abkürzung.
7. **UI/UX:** Mindestens 5 moderierte Walkthroughs mit Personen ohne Thunder-/CLI-Erfahrung; First-Run, fehlender Token, schlechtes Netzwerk, ungültiger GPU-Preis, Pod existiert, Download 401/403/404, Abbruch, Snapshot/Löschen. Maßzahl: Aufgabe und sichere nächste Handlung ohne fremde Hilfe erklärbar.
8. **Techniktests:** Windows 10/11 x64 auf realer Maschine, DPI 100/125/150/200 %, Tastatur/Screenreader, privater Repo-API-Fehler, CLI-Versionsabweichungen, echte SSH-Key-Provisionierung, authentifizierter SSH-Tunnel, Download-Parallelität und CUDA-/ComfyUI-End-to-End. Kostenpflichtige Live-Tests **nur nach gesonderter Freigabe**; keinen zweiten Pod dafür anlegen.
9. **Releaseprozess:** externer Sicherheitsreview und nachvollziehbares Sign-off, signierter Installer bzw. gesicherte Integritätskette, alle P0 geschlossen, alle P1 korrigiert oder formal mit klar begrenztem Alpha-Risiko akzeptiert; Draft-PR erst danach zur Freigabe.

## 6. Fazit und Priorisierung

**Gut integriert:** privater GitHub-Lesekatalog, aktuelle 18 Workflow-Manifeste, vorhandener parallelisierter Download-Bootstrap, offizielle CLI-Befehle, Windows-GUI/Installer-Mock-Build, grundlegendes SSH-Trust-Modell.

**Noch nicht belastbar:** kostenfreie und korrekte Budgetgrenze, vollständiger neuer/alter Pod-Preflight, unveränderlicher Remote-Code, dauerhaftes Resume, modellgenauer Bereitstellungsabschluss, kontrollierbare Fehlersituation für Laien und echte End-to-End-Funktion.

**Entscheidung:** `v0.1.0-alpha.13` als experimentelle Alpha beibehalten, **kein Merge auf main, kein Stable-Release**. **Zuerst RT-01 und RT-02**, dann RT-03 bis RT-08 und UX-01/03/04/05. Die statische Eigenprüfung wird **nicht** als unabhängige Red-Team-Abnahme deklariert.
