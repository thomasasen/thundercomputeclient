# Red-Team-Qualitätssicherung: Ein-Klick-Thunder-Pod, vollständige ComfyUI-Bereitmeldung

**Prüfdatum:** 16. November 2026  
**Geprüfter Desktop-Stand:** `feature/windows-desktop-client`, Commit `b02df8ac1c48e2623c292e53d7f7301caa9243d8`  
**Prüfgegenstand:** Windows WPF-Client, GitHub-Katalog, Thunder CLI/Console-Vertrag, privater Thunder-Bootstrap, Modell-/Node-Installationslog, abschließende ComfyUI-Readiness  
**Prüfmethode:** unabhängige statische Red-Team-Sichtung mit Abgleich gegen die tatsächlich geladenen Upstream-Dateien und offiziellen Provider-Dokumente; Auswertung der Windows-CI; adverse Szenarien, kein kostenpflichtiger Cloud-E2E-Test  
**Freigabeurteil:** **NICHT FREIGEGEBEN** für einen nutzerseitig verlässlichen, unbeaufsichtigten RunPod-ähnlichen Ein-Klick-Start. Draft-PR beibehalten, kein Stable-Release.

## 1. Ziel und überprüfbare Definition von „fertig“

1. Nutzer wählt Workflows, GPU, Disk und akzeptiert **nach vollständiger, nachgewiesener Preiskalkulation** einen kostenpflichtigen Pod.
2. Genau **ein** Pod wird erstellt; die Pod-ID und das Ergebnis sind auch nach CLI-/Netzwerkfehlern rekonstruierbar.
3. SSH-Zugang nutzt offiziell dokumentierte Schlüsselflüsse; ein Vertrauensanker für den Hostfingerprint ist verifizierbar.
4. Der **vorher ausgewählte und validierte Commit** ist genau der auf dem Pod ausgeführte Commit.
5. Der Remote-Bootstrap läuft robust weiter, auch bei GUI-/SSH-Unterbrechung; nach Wiederverbinden werden Jobstatus und vollständige Loghistorie wieder sichtbar.
6. Für **jede ausgewählte** Modelldatei stehen Name, erwartete Größe, übertragenes Volumen, Fortschritt, Retry/Fehler und Endzustand fest. Für Node-Pakete sind Installation und geladene Klassen nachvollziehbar.
7. Ein separates Audit prüft alle Modellziele/HF-Snapshots, pinned Nodes, importierte Workflows, die betriebsbereite ComfyUI-API sowie alle benötigten Node-Klassen; ein geeigneter Inferenz-Smoketest ist die separate letzte Qualitätsstufe.
8. Nur danach und nur einmal erscheint „ComfyUI ist einsatzbereit“, mit verfügbarer Aktion zum Öffnen des privaten SSH-Tunnels. Keine Erfolgsmeldung nach bloßem HTTP 200.

## 2. Herkunft und Quellenprüfung

| Prüfbereich | Offizielle / tatsächliche Referenz | Ergebnis |
|---|---|---|
| Pod-Erstellung | Thunder [CLI: Creating Instances](https://github.com/Thunder-Compute/thunder-compute-documentation/blob/main/cli/operations/creating-instances.mdx), `tnr create --gpu … --vcpus … --template base --disk …` | CLI-Form grundsätzlich dokumentiert; tatsächliche verfügbare Ressourcenkombinationen live zu prüfen |
| CLI-/SSH-Verbindung | Thunder [Connecting to Instances](https://github.com/Thunder-Compute/thunder-compute-documentation/blob/main/cli/operations/connecting-to-instances.mdx), [Manual SSH](https://github.com/Thunder-Compute/thunder-compute-documentation/blob/main/guides/ssh-on-thunder-compute.mdx) | Thunder empfiehlt gespeicherten öffentlichen SSH-Schlüssel **vor** Create oder bewusstes Add-Key nach Create; Client verwendet fehleranfälliges `tnr connect --json` nach Create |
| Preisprüfung | [Thunder Pricing](https://www.thundercompute.com/pricing) (14.11.–16.11.2026 geprüft) | Zusätzliche vCPUs: 0,04 USD/vCPU-h; Client-Schätzung berücksichtigt nur GPU und zusätzliche Disk |
| GitHub-Revision | Desktop `GitHubWorkflowSource.cs`; private `thunder/bootstrap.sh`; `scripts/pod_entry.py` | Client pinnt Auswahl, Adapter erzwingt `REPO_REF=main` und lädt das Entrypoint-Skript mit `?ref=main` |
| Modellereignisse | private `scripts/download_monitor.py`, `scripts/bootstrap.py`, `scripts/early_workers.py` | Authentische `[DOWNLOAD-STATUS]`-Events pro Modell vorhanden; vollständige Historie im Client nicht gesichert |
| Readiness | `DeploymentReadinessGate.cs`, `RemoteReadinessAudit.cs`; private `scripts/readiness_probe.py`, `scripts/manifest.py` | Gute Fail-Closed-Absicht, aber kein real ausgeführter Remote-Audit-Test und kein Recheck eines laufenden ComfyUI |
| ComfyUI Custom Nodes | private `scripts/bootstrap.py`: `--disable-all-custom-nodes`, `--whitelist-custom-nodes`; ComfyUI `/object_info` | Korrektes Ziel, tatsächliche Module-/Klassen-Zuordnung für alle Workflows nicht mit Runtime-Fixtures verifiziert |

**Ausdrückliche Grenze:** Die aktuelle Prüfung kann die offizielle Dokumentation und den Repo-Code vergleichen, aber nicht das Verhalten der Live-Thunder-API, der GPU, des Remote-Dateisystems, der Netzwerkanbieter oder das Rendern mit echten Gewichten bestätigen. Es wurde **kein** Pod neu erstellt, gelöscht oder verändert.

## 3. Bestätigte Befunde und Schweregrad

### P0-01: Kostenfreigabe unterschätzt vCPU-Gebühren

**Code:** `src/ThunderClient/Core/Models.cs:72–78`, `AutoProvisioning.cs:60–65`, `ThunderCli.cs:103–117`.  
**Nachweis:** Der Client berechnet `GPU-Preis × Anzahl + Disk-Zuschlag`, ohne die von Thunder separat berechneten zusätzlichen vCPUs. Beispiel L40, 12 vCPUs, 149 GB Disk: `0,79 + 49×0,219/730 ≈ 0,805 USD/h` clientseitig vs. `0,79 + 6×0,04 + 49×0,219/730 ≈ 1,045 USD/h` nach veröffentlichten Thunder-Preisen. Ein Nutzerlimit 0,85 USD/h schützt deshalb nicht. Preise/Spezifikationen sind zudem nicht live validiert.  
**Folge:** kostenpflichtiger Pod mit tatsächlich höherer Rate trotz ausdrücklicher Bestätigung.  
**Abhilfe:** providerbasierte GPU-/vCPU-/Disk-Tarife und inkludierte vCPUs, serverseitiges Angebot beziehungsweise klar verifizierte aktuelle Tarife; finale Summe und identische Hardwarekonfiguration vor Create bestätigen; fehlende Preisbasis fail-closed.  
**Ticket:** [#3](https://github.com/thomasasen/thundercomputeclient/issues/3).

### P0-02: SSH-Key-API-Timeout nach bezahltem Create

**Code:** `ThunderCli.GetSshAsync`: `tnr connect <id> --json`; Timeout-Behandlung `ThunderSshKeyApiTimeoutException`.  
**Nachweis:** Der dokumentierte Ablauf unterstützt vorher gespeicherte SSH-Schlüssel. Der Client hängt nach dem Create von der nachträglichen Registrierung über `/add_key` ab; ein 30-Sekunden-Timeout wurde bereits im Projekt beobachtet.  
**Folge:** Pod verursacht Kosten; weder Bootstrap noch Modelle starten. Die Operation kann serverseitig trotzdem erfolgt sein und darf nicht blind wiederholt werden.  
**Abhilfe:** vorab registrierter, bei Create zugeordneter öffentlicher Schlüssel über dokumentierte Console/API-Möglichkeiten, exakte Korrelation des Pod-Status, idempotente Wiederaufnahme.  
**Ticket:** [#8](https://github.com/thomasasen/thundercomputeclient/issues/8), [#7](https://github.com/thomasasen/thundercomputeclient/issues/7).

### P0-03: Kein dauerhafter Remote-Job und kein sicherer Resume

**Code:** `SshService.cs:275–280,325–345`, `ProcessRunner.cs:119–162`, `MainViewModel.cs:867–944`; Upstream `scripts/bootstrap.py` ersetzt seinen Prozess am Ende mit ComfyUI.  
**Nachweis:** Der Desktop hält einen einzigen bis zu zwölf Stunden dauernden SSH-Prozess offen und leitet dessen stdout direkt an die GUI. Bei Abbruch werden der lokale SSH-Prozess und ggf. die Remote-Prozesskette unterbrochen. Kein persistierter, eindeutig identifizierter Deploy-Job oder Logcursor; kein Reattach.  
**Folge:** keine verlässliche Laufzeit- oder Fertigmeldung nach GUI-Schließen, Verbindungsverlust oder langer Installation, während GPU-Kosten weiterlaufen können.  
**Abhilfe:** Remote-Service/Supervisor, PID/Run-ID, persistentes Statusfile und append-only Logs, idempotentes Reattach, per-Pod-Lock und klar getrennte Abbrechen-/Pod-löschen-Aktionen.  
**Ticket:** [#4](https://github.com/thomasasen/thundercomputeclient/issues/4), [#12](https://github.com/thomasasen/thundercomputeclient/issues/12).

### P1-04: Repo-Revision driftet nach der bezahlten Instanzerstellung

**Code:** `GitHubWorkflowSource.cs` pinnt einen Commit beim Kataloglesen; `SshService.BootstrapAsync` exportiert `REPO_REF=main`; privates `thunder/bootstrap.sh:15–20,66–67` lädt `pod_entry.py?ref=main`, `scripts/pod_entry.py:76–93,138–145` löst `main` erneut auf.  
**Nachweis:** Der vorher geprüfte Commit muss nicht der nach Pod-Create ausgeführte Commit sein. `DeploymentReadinessGate.cs:59–66` entdeckt das und blockiert die Erfolgsmeldung, aber **erst nach** Create/Bootstrap.  
**Folge:** nicht reproduzierbarer Start, bezahlte Fehlstarts trotz ursprünglich gültiger Auswahl, zusätzlicher Supply-Chain-Risikoabschnitt.  
**Abhilfe:** exakt ausgewählten Commit **vor** Create einfrieren und vom Entrypoint/Bootstrap ausführen lassen; Commitvergleich bereits vor großen Downloads. Keine Änderung am Upstream aus diesem Client-Projekt.  
**Ticket:** [#4](https://github.com/thomasasen/thundercomputeclient/issues/4), [#11](https://github.com/thomasasen/thundercomputeclient/issues/11).

### P1-05: Bei laufendem ComfyUI wird das Audit übersprungen

**Code:** `MainViewModel.cs:848–856`.  
**Nachweis:** Bei `/system_stats=200` wird unmittelbar `return` ausgeführt. Ein Remote-Readiness-Audit findet nicht statt, obwohl der eigentliche Audit-Code existiert.  
**Folge:** Selbst ein korrekt vorinstallierter Pod erhält keine neue geprüfte Fertigmeldung; eine veränderte Auswahl wird nicht vollständig verglichen.  
**Abhilfe:** read-only Audit eines laufenden Pod immer durchführen, unabhängig davon, ob Bootstrap erneut nötig ist; bei fehlenden Modellen ausdrücklich entscheiden lassen, was zu installieren ist.  
**Ticket:** [#5](https://github.com/thomasasen/thundercomputeclient/issues/5), [#11](https://github.com/thomasasen/thundercomputeclient/issues/11).

### P1-06: Downloadhistorie ist nicht lückenlos, Prozess wird durch Logvolumen abbrechbar

**Code:** `MainViewModel.cs:1152–1159` hält max. 600 Logzeilen, `ProcessRunner.cs:119–142` sammelt stdout/stderr im RAM und wirft je Stream nach über 4 Millionen Zeichen.  
**Folge:** Startmeldungen, Dateiergebnisse und Fehlerdetails werden in längeren Installationen unwiederbringlich aus der GUI verdrängt. Sehr lange stdout-Ströme führen unabhängig von Pod-/Modellgesundheit zu einem Fehler im lokalen Client.  
**Abhilfe:** persistente, rotierende Logdateien, cursorbasierter Live-Stream, beliebig lange History mit Paging, keine vollständige Prozessausgabe im Memory-StringBuilder, klare Node-Event-Schemata.  
**Ticket:** [#12](https://github.com/thomasasen/thundercomputeclient/issues/12).

### P1-07: Das eigentliche Python-Audit ist in CI nicht funktional ausgeführt

**Code:** `tests/ThunderClient.Tests/RemoteReadinessAuditTests.cs:10–51`.  
**Nachweis:** Die Tests prüfen generierte Zeichenfolgen und vorgefertigte `CommandResult`-Ausgaben, nicht die Python-Prüfung auf einem realistischen Dateibaum mit einem gemockten ComfyUI-HTTP-Server. Der `object_info`-URL-Test ersetzt zunächst `' + path` im Skript durch `object_info` und bestätigt dann das damit konstruierte Fragment.  
**Folge:** 90/90 bestandene Mocktests können wichtige Remote-Laufzeit- oder Zuordnungsfehler nicht entdecken.  
**Abhilfe:** embedded Python in isolierter Fixture-Umgebung laufen lassen, mit echten Repo-Manifesten und simuliertem `/object_info`; C#-Dispatcher-/SSH-Replay-Tests ergänzen.  
**Ticket:** [#11](https://github.com/thomasasen/thundercomputeclient/issues/11).

### P1-08: File-/Node-Audit kann Betriebsbereitschaft nicht vollständig beweisen

**Code:** `RemoteReadinessAudit.cs:95–112,173–214`.  
**Nachweis:** Modelldateien müssen existieren und nicht leer sein, aber SHA256/Inhaltsintegrität oder einmalige minimale Inferenz werden nicht geprüft. Der Upstream `scripts/bootstrap.py:261–276,319–325` verwendet je nach Modell Provenienz-/Dateisystem-Fingerprints und weist explizit auf `ohne SHA256-Prüfung` hin. Module werden anhand des erwarteten `custom_nodes.<Ordner>`-Namens verifiziert; einzelne UI-only Node-Klassen sind nur über eine kurze Liste ausgenommen.  
**Folge:** Falsch-positiver „einsatzbereit“-Status bei inhaltlich unbrauchbarem Gewicht ist möglich; Falsch-Negative bei abweichend registrierten Modulen/UI-Nodes müssen an echten Fixtures ausgeschlossen werden.  
**Abhilfe:** kryptographische Prüfsumme dort, wo im Manifest spezifiziert und zulässig; modell-/workflowspezifischer GPU-Smoketest mit kontrolliertem Ressourcenbudget; realer `object_info`-Abgleich sämtlicher Workflow-Varianten.  
**Ticket:** [#11](https://github.com/thomasasen/thundercomputeclient/issues/11).

### P1-09: Remote-Workspace und vorhandene Pod-Hardware werden nicht vollständig abgeglichen

**Code:** `RemoteReadinessAudit.cs:67–71` hardcodiert `~/comfyui-workspace` trotz im Thunder-Adapter konfigurierbarem `WORKSPACE_DIR`. `MainViewModel.cs:760–770` prüft bei Reuse nur einen groben positiven Disk-Wert und Status, nicht GPU-VRAM, freien Speicher, CUDA und Modell-Restbedarf.  
**Folge:** zulässige abweichende Workspaces schlagen beim Audit fehl; vorhandene Pods können Hardware-/Speicheranforderungen der Auswahl verfehlen und kostenpflichtige Downloads starten.  
**Abhilfe:** tatsächlichen sicher validierten Workspace aus dem Remote-Jobstatus übernehmen; VRAM/CUDA/freie GB und vorhandene Modelldateien vor installation prüfen.  
**Ticket:** [#5](https://github.com/thomasasen/thundercomputeclient/issues/5), [#9](https://github.com/thomasasen/thundercomputeclient/issues/9), [#11](https://github.com/thomasasen/thundercomputeclient/issues/11).

### P1-10: Erfolgreiches Create kann seine Pod-ID verlieren

**Code:** `ThunderCli.cs:114–123`.  
**Nachweis:** Auf Exitcode 0 wird das `tnr create --json`-Ergebnis ignoriert und `tnr status` erneut abgefragt. Ist die Liste direkt danach leer, veraltet oder mehrdeutig, wird die tatsächliche neue Pod-ID nicht aus der erfolgreichen Antwort übernommen.  
**Abhilfe:** strukturierte, redigierte Erfassung der Create-Antwort, genaue Instanz-ID/UUID, anschließende reconciliation ohne Rohlogging möglicher privater Schlüsselfelder.  
**Ticket:** [#10](https://github.com/thomasasen/thundercomputeclient/issues/10).

### P1-11: Der lokale Katalog erkennt bestimmte Modellquellen-Konflikte nicht

**Code:** `WorkflowCatalog.cs:33–51,108–125` gegen private `scripts/manifest.py:266–295`.  
**Nachweis:** Bei HF-Downloads vergleicht der Client als Modellidentität nur `repo/file` und übersieht abweichende `revision`-Pins. Bei `hf_snapshot` entstehen ohne `version_id`/`sha256` identische `SourceId`-Werte `hf_snapshot::` selbst bei völlig verschiedenen `repo`, `revision` und `files`. Der Upstream-`manifest.resolve` vergleicht dagegen die vollständigen Provenienzattribute. Zwei ausgewählte Workflows können deshalb clientseitig `Collisions == 0` melden und erst **nach** bezahltem Create vom Remote-Bootstrap als `Modellkonflikt` abgelehnt werden. Der Desktop prüft auch nicht alle vom Upstream verpflichtenden README-/Graph-/Node-/Setup-Konsistenzen vor Create.  
**Abhilfe:** manifestidentische, quellgetreue Katalogprüfung vor kostenpflichtigem Start; Fixture-Tests für unterschiedliche Pins, HF-Snapshot-Dateilisten, fehlendes README und abweichende Node-Commits. Kein Duplizieren divergender Installationslogik.  
**Ticket:** [#13](https://github.com/thomasasen/thundercomputeclient/issues/13), [#9](https://github.com/thomasasen/thundercomputeclient/issues/9).

## 4. Explizite adversariale Abnahmetests

| ID | Gegenprobe | Erwartetes Verhalten | Aktueller Nachweis |
|---|---|---|---|
| RT-01 | HTTP 200, mindestens ein Modell fehlt | Keine Fertigmeldung | Mocktest vorhanden, kein echtes Remote-Audit |
| RT-02 | Vollständige Auswahl, alle Modelle/Nodes vorhanden | Einmalige Fertigmeldung und ComfyUI öffnen | Mocktest für Gate; kein Live-Pod |
| RT-03 | Laufendes ComfyUI bei Wiederverwendung | Read-only Vollständigkeitscheck statt sofortigem Return | **Fehlschlag aus Code ableitbar** |
| RT-04 | Repo-main geändert nach Katalogladen | Keine Ausführung unbekannter Revision | **Vorab-Pinning fehlt** |
| RT-05 | L40/12 vCPU/149 GB, Grenze 0,85 USD/h | Vor kostenpflichtigem Create blockieren | **Fehlschlag rechnerisch belegt** |
| RT-06 | `/add_key` timeout, Pod existiert | Kein zweiter Create; sichere Reconciliation | Teilweise geschützt, automatischer Start blockiert |
| RT-07 | Erfolgreiches Create, `status` veraltet | Pod-ID aus Create erhalten | **Fehlverhalten aus Code ableitbar** |
| RT-08 | 700 Logzeilen | Früheste Ereignisse exportierbar und nachlesbar | **Fehlverhalten aus Code ableitbar** |
| RT-09 | 4,1 MB stdout während langer Installation | Stream bleibt betriebsbereit | **Fehlverhalten aus Code ableitbar** |
| RT-10 | GUI geschlossen, ComfyUI läuft und Modelle werden geladen | Remote-Job läuft unabhängig, Reattach mit Historie | Nicht implementiert |
| RT-11 | 12-Stunden-Sitzungsgrenze erreicht | Remote-Dienststatus unabhängig vom SSH-Prozess | Nicht implementiert |
| RT-12 | Python-Audit mit echter temporärer Manifest-/Node-/Modell-Fixture | `[THUNDER-READINESS]` nur bei gültiger Fixture | Noch nicht getestet |
| RT-13 | Custom-Node-Import fehlt trotz gepinntem Ordner | Keine Fertigmeldung | Logik vorhanden, kein Runtime-Fixture-Test |
| RT-14 | Sondernode im UI-Graph, Backend-Klasse nicht erforderlich | Kein falscher Readiness-Abbruch | Nicht getestet |
| RT-15 | HF-Snapshot nur teilweise vorhanden | Keine Fertigmeldung | Logik vorhanden, nicht ausgeführt |
| RT-16 | Fremdes `WORKSPACE_DIR` innerhalb erlaubten HOME-Unterordners | Tatsächlichen Workspace prüfen | **Hardcoded Audit-Pfad kollidiert** |
| RT-17 | Fehlende Modell-Inferenz trotz nichtleerer Gewichtsdateien | „Datei vorhanden“ nicht als Inferenzbestätigung ausgeben | Begrenzung dokumentiert; kein Inferenztest |
| RT-18 | Bestehender Pod hat zu wenig freien Platz oder VRAM | Vor teuren Downloads blockieren | Nicht ausreichend implementiert |
| RT-19 | Spezifischer Custom-Node/Setup-Fehler | Fehler inkl. Name, Phase, Abhilfe und Logverlauf | Teilweise unstrukturierte Logs |
| RT-20 | Zwei GUI-Prozesse und verlorene Verbindung | Ein Pod, ein Remote-Job, keine zweite Installation | Lokaler Create-Lock vorhanden; globaler Deploy-/Reattach-Nachweis fehlt |
| RT-21 | ComfyUI-`/object_info` gibt 404/Timeout | Keine Fertigmeldung, späterer Retry möglich | Fail-Closed-Logik vorhanden; Retry/Reattach fehlt |
| RT-22 | GitHub-/Civitai-/HF-Token in Prozess-/Fehlerdaten | Kein Token in Log, Clipboard, URLs, CLI-Argumenten | Statische Sanitisierung vorhanden; kein kompletter adversarial Leak-Test |
| RT-23 | Zwei HF-Snapshots am gleichen Ziel mit unterschiedlichen GitHub/HF-Revisions | Konflikt vor kostenpflichtigem Create erkennen | **Desktop-Identität ignoriert Snapshot-Revision/Dateiliste** |
| RT-24 | HF-Quellen mit identischem Repo/Dateipfad, aber verschiedener Revision | Konflikt vor kostenpflichtigem Create erkennen | **Desktop-Identität ignoriert HF-Revision** |

## 5. Windows-CI und Testabdeckung

- [Windows-Workflow 43776172256](https://github.com/thomasasen/thundercomputeclient/actions/runs/43776172256) auf exakt `b02df8ac`: **success**.
- `dotnet test`: **90 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen**.
- WPF-GUI-Smoke, Publish, Installer und SHA-Erzeugung im Workflow erfolgreich.
- **Nicht abgedeckt:** echte Thunder-Instanz, echtes `tnr connect`, wirkliche SSH-Key-Registrierung, echte GPU-/CUDA-Erkennung, Modelltransfers von Anbietern, Remote-Python-Audit auf gepinnten Manifesten und Inferenz.

Ein grüner Mocklauf ist ein notwendiges, aber kein hinreichendes E2E-Freigabekriterium.

## 6. Reparatur- und Freigabereihenfolge

1. **Sofortige P0-Gates:** vollständige Preis-/vCPU-Kalkulation, SSH-Key-Vorregistrierung bzw. dokumentiert sichere API-Zuordnung, exakte Create-ID-Reconciliation; vor diesen Punkten kein nutzerseitig unbeaufsichtigtes Create.
2. **Robuster Deployment-Lebenszyklus:** revision-gepinnte Quelle **vor** Kostenstart, Remote-Supervisor, Run-ID und Restart-/Reattach-Verhalten, persistente Status-/Logdateien ohne stdout-Abbruch.
3. **Katalog- und Readiness-Prüfung funktionsfähig machen:** Vollständige Manifest-Identität und Quellkonsistenz **vor** kostenpflichtigem Create nachweisen; Remote-Audit auch bei bereits laufendem ComfyUI; integrierte Python-Fixture-Tests; modell-/nodegenauer Logvertrag; vollständige Runtime-Manifest-/Backend-Abgleiche.
4. **Praktische Ende-zu-Ende-Freigabe:** kontrollierter echter Thunder-Pod-Test **nur nach gesonderter kostenpflichtiger Freigabe**, inklusive bestätigter GPU-Specs, Modell-/Node-Installation, ComfyUI-Tunnel, Abbruch/Reattach und repräsentativem Inferenz-Smoketest.
5. **Erst danach** unabhängiges Security-Review, PR-Merge und Release.

**Ausdrücklich keine Freigabe durch dieses Dokument:** keine Stable-/Produktionsfreigabe, keine automatische Übernahme von Kostenausgaben, kein neues Cloud-Deployment, keine Änderung am privaten Upstream.
