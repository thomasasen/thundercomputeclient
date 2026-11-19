# Thunder Compute Client

Nativer Windows-Desktop-Client (.NET 10/WPF) für ComfyUI-Workflows aus dem **privaten**, unveränderten GitHub-Repository `thomasasen/runpod-ai-workflows`.

**Status: Release v0.1.8 für Windows x64. Installierbar, aber noch nicht durch einen vollständigen Thunder-/ComfyUI-/GPU-End-to-End-Test produktionsvalidiert.** Die Windows-CI prüft Kompilierung, Mock-/Regressionstests, den tatsächlichen WPF-Fensterstart mit allen acht Bereichen und den Inno-Setup-Installer. Kein echter Thunder-/GPU-/SSH-/Modelldownload-End-to-End-Test.

**SSH-Diagnose-Hotfix v0.1.1:** Eine nicht bestätigte SHA256-Hostfingerprint-Abfrage wird getrennt von technischen OpenSSH-/KEX-/Zeitüberschreitungsfehlern behandelt; die ursprüngliche, bereinigte technische Ursache erscheint im Fehlerdialog. Der Client akzeptiert weiterhin niemals ungeprüfte Hostschlüssel. Eine unabhängige, vertrauenswürdige Verbindung zum Pod ist für die erste Fingerprint-Freigabe erforderlich.\n\n## Dark-Mode-Kontrastfix (v0.1.8)

Die Oberfläche verwendet im Dunkelmodus ausdrücklich hellen Text auf dunklen Flächen und kontrollierte Kontrastfarben bei primären sowie destruktiven Buttons. Textblöcke, Überschriften, Labels, Dropdown-/ComboBox-Einträge, GPU- und Listenzeilen sowie Tooltips folgen der jeweils aktiven Theme-Palette. Der Windows-GUI-Smoke-Test kontrolliert alle acht Bereiche auch bei Dunkelmodus, einschließlich Dropdown- und Button-Kontrasten. Rein visuelle Darstellung; keine Änderung an Thunder-, Download- oder Kostenlogik.

## Oberfläche: Material-Icons und komfortablere Downloads-Tabelle

Die Windows-Oberfläche verwendet lokal eingebettete, lizenzierte **Google Material Icons** neben den unveränderten Button-Beschriftungen und in den Statusanzeigen. Unter **Downloads & Logs** sind alle drei Tabellenspalten per Headerklick sortierbar (Prozentwerte numerisch), Spaltenbreiten per Maus veränderbar und der Trenner zwischen Tabelle und Log lässt sich nach oben/unten ziehen. Farbige Statusabzeichen sind auch im Hell-/Dunkelmodus lesbar. Implementierung, Farblegende und Lizenz: [UI-Bedienung und Material-Icons](docs/UI_MATERIAL_ICONS_AND_DOWNLOADS.md).

## Pod wiederherstellen und Downloads überwachen (v0.1.2)

**Unter Instanzen:** `Pod wiederherstellen (Snapshot) …` sichert die vorhandene persistente Instanzdisk über den offiziell dokumentierten Thunder-Snapshot-Befehl. Der Client liest unmittelbar vor dem Snapshot den Thunder-Status neu ein und prüft die ausgewählte Instanz-ID, UUID und RUNNING-Status. **Kein In-Place-Pod-Neustart**: Thunder dokumentiert keinen nativen Start/Stop- oder Reboot-Befehl für dieselbe GPU-Instanz. Den Snapshot nach Status READY in der [Thunder-Podverwaltung](https://console.thundercompute.com/) **als neue kostenpflichtige Instanz** wiederherstellen, Daten und ComfyUI prüfen und erst anschließend den ursprünglichen Pod ausdrücklich löschen. Kein automatischer Pod-Create/Delete und kein `sudo reboot`. Laufende Schreibvorgänge vorher beenden; Snapshots sind kein garantierter externer Backup-Ersatz. Eine Momentaufnahme laufender Downloads ist gegebenenfalls inkonsistent.

**Unter Downloads & Logs:** Die Modelldownload-Tabelle hat ihren eigenen vertikalen/horizontalen Scrollbereich (mit Virtualisierung), zeigt echte `[DOWNLOAD-STATUS]`-Zustände in dynamischen Farben: Grün für `GELADEN`/`VORHANDEN`, Akzent für `LAEUFT`/`FORTSCHRITT`, Warnfarbe für `RETRY`, Fehlerfarbe für `FEHLER`, neutral für `WARTET`. Unter **Instanzen** sowie **Downloads & Logs** führt ein direkter Link zur offiziellen Thunder-Podverwaltung.

## SSH-Authentifizierung bei neuen Pods: Timeout-Retry (v0.1.5)

Der erste SSH-Anmeldetest (`ssh -T ubuntu@HOST true`) kann kurz nach dem Pod-Start trotz erfolgreicher SSH-Fingerprint-Prüfung durch sein eigenes 25-Sekunden-Limit scheitern. Die bereits vorhandene beschränkte Wiederholungslogik behandelt jetzt auch Prozess-Timeouts und interne Command-Cancellation als **temporäre Authentifizierungsfehler**, solange die ausdrückliche Nutzer-Cancellation nicht ausgelöst wurde. Maximal sechs getrennte read-only-Tests mit fünf Sekunden Abstand; keine erneute SSH-Schlüsselregistrierung, keine Änderung an `known_hosts`, keine neue Pod-Erstellung. Hostkey-Wechsel werden weiterhin sofort und sicher abgelehnt. Bei dauerhaftem Fehlschlag werden Pod-Erhalt, Fehlertext und Fortsetzung mit derselben Instanz angezeigt.

Das Protokoll vom 18.11.2026 zeigte außerdem, dass nach dem einmaligen SSH-Timeout der bestehende A100-Pod später erfolgreich den Remote-Bootstrap mit elf Modellgewichten startete. Eine erfasste Momentaufnahme von 13:43:41 Uhr bestätigt drei von drei Civitai- und drei von acht Hugging-Face-Downloads, aber noch keine abgeschlossene ComfyUI-Inferenz.

## MiniMax H3 Image: A100-API-Kennung korrigiert (v0.1.4)

Die offizielle Thunder-CLI verwendet für die 80-GB-A100 die Providerkennung `a100xl`, mit dem Spec-/Pricing-Schlüssel `a100xl_x1`. Der lokale GUI-Eintrag `a100` bleibt aus Kompatibilitätsgründen mit gespeicherten Einstellungen erhalten. **Beide Read-only-Liveprüfungen und der spätere `tnr create --gpu`-Parameter verwenden nun konsequent `a100xl`.** Es gibt keine stillschweigende Fallback-Umgehung bei fehlenden Anbieter-Spezifikationen.

Für acht vCPUs berücksichtigt die konservative A100-Listenpreisschätzung jetzt **vier zusätzliche vCPUs** gegenüber der kleinsten offiziellen CLI-Spezifikation. Die Standard-Schätzpreisgrenze neuer Installationen beträgt 1,35 USD/h; ein bereits gespeichertes niedrigeres Limit bleibt bis zur expliziten Zustimmung unverändert. Es folgen weiterhin eine **Live-Quote** und eine separate **kostenpflichtige Pod-Freigabe**. Bei höheren aktuellen Anbieterpreisen wird die Erstellung blockiert, bis die Preisgrenze ausdrücklich angepasst ist. Ein echter bezahlter Thunder-/ComfyUI-Inferenztest steht aus.

## MiniMax H3 Image: Thunder-GPU-Auswahl (v0.1.3)

Die dokumentierte 80-GB-VRAM-Orientierung für den unveränderten 8-MP-Canvas ist ein **Thunder-spezifischer konservativer Planungswert**, keine gemessene Mindestanforderung. Der Client bietet nun **A100 mit 80 GB standardmäßig aktiviert** an; die **H100 mit 80 GB** ist zusätzlich, zunächst deaktiviert, konfigurierbar. Die günstigeren 48-GB-GPUs bleiben für diesen Canvas unterhalb der konservativen Auto-Deploy-Grenze und sind **nicht als 8-MP-kompatibel getestet**.

Für Neuinstallationen beträgt die voreingestellte **Schätzpreisgrenze 1,25 USD/h**. Bei bereits gespeicherten 0,85 USD/h kann **Neue Instanz → Planungswerte übernehmen** nach ausdrücklicher Zustimmung die Schätzpreisgrenze anpassen. Die Live-Preis-/Spezifikationsprüfung und die gesonderte kostenpflichtige Pod-Freigabe bleiben zwingend. Der geplante Diskwert für alle 11 H3-Image-Gewichte liegt bei **211 GB**. Es wird **kein** Modell ausgelassen, nur um den Bedarf künstlich zu senken.

## Ein-Klick-Bereitstellung

Nach dem automatischen Laden des privaten Katalogs auf **Workflows** einen oder mehrere Workflows auswählen und **„Automatisch bereitstellen: Pod + ComfyUI + Modelle …“** anklicken. Alternativ unter **Neue Instanz** die Hardware vorher anpassen und **„Pod + Modelle automatisch starten …“** wählen.

1. Der Client berechnet die deduplizierte Modellgröße aus den authentifizierten `model.json`-Manifesten sowie eine Gesamtdisk mit der dokumentierten pauschalen Reserve. README-VRAM-Angaben sind **RunPod-Referenzen, keine Thunder-Messungen**. Er berücksichtigt nur bereits aktivierte GPUs und erhöht **niemals automatisch die Preisgrenze**. Fehlende Größen, widersprüchliche Modelldateien und gesondert zu bestätigende Manifestautorisierungen blockieren den automatischen Start.
2. **Vor der kostenpflichtigen Erstellung** fragt der Client die von der offiziellen Thunder-CLI verwendeten Read-only-Endpunkte `/v2/pricing` und `/v2/specs` ab. GPU, vCPU-Optionen, VRAM, Disk und die **vollständige** geschätzte Stundenrate werden gegen die freigegebene Preisgrenze geprüft. Fehlen die Anbieterinformationen oder wird die anonyme Abfrage nicht zugelassen, ist die automatische Neuerstellung gesperrt. Erst nach der bestätigten GUI-Kostenfreigabe wird ein Pod angelegt. Die Anbieterquote ist **kein serverseitiges Budgetlimit**. **Existiert bereits genau ein Pod, wird dieser nach eigener Zustimmung wiederverwendet.** Bei mehreren Pods wird aus Sicherheitsgründen abgebrochen, statt versehentlich den falschen Pod zu wählen.
3. Der Client wartet auf den vom Anbieter gemeldeten Status `RUNNING` mit gültiger SSH-IP und Port, bereitet den SSH-Schlüssel über die offizielle `tnr`-CLI vor und verlangt bei einem neuen Host die **unabhängige ED25519-Fingerprint-Bestätigung**. Ist SSH nicht bereit, wird **kein neuer GPU-Pod** erzeugt.
4. Anschließend startet der Client den vorhandenen `thunder/bootstrap.sh` aus der privaten GitHub-API im per SSH geöffneten Pod. Der Client bindet den Entrypoint und den Snapshot **an den vorher ausgewählten GitHub-Commit**; falls sich die erwartete Adapterstruktur ändert, wird nicht still auf `main` zurückgefallen. Der Upstream-Bootstrap installiert ComfyUI, seine Nodes und die ausgewählten Workflows. Er startet die **bestehenden parallelen Modell-/Node-Downloads** mit adaptivem, auf **maximal vier gleichzeitige Datei-Jobs** begrenztem Download-Scheduler. `Downloads & Logs` zeigt die Echtzeitprotokolle, Modelldateinamen, Transferfortschritte und die vom Upstream gemeldete ComfyUI-HTTP-Bereitschaft. **HTTP-Bereitschaft ist ausdrücklich keine vollständige Erfolgsfreigabe.** Ein bereits antwortender ComfyUI-Dienst wird nicht ungefragt durch einen zweiten Bootstrap überlagert.
5. Unter **Instanzen** kann ComfyUI wie bisher über den privaten SSH-Tunnel geöffnet werden. Für diesen Modus wird **kein öffentlicher ComfyUI-Port** eingerichtet.

**Vollständige Bereitmeldung (v0.1.2; noch nicht mit echten Modellen live validiert):** Der Client sammelt die echten `[DOWNLOAD-STATUS]`-Ereignisse des zentralen Bootstraps und gleicht die Endzustände mit den zuvor authentifiziert ausgewählten Modellen ab. Er fordert außerdem die erfolgreich durchlaufenen Node-/Workflow-/CUDA-Phasen, den sauberen Dependency-Audit und den Nachweis, dass der Remote-Bootstrap denselben GitHub-Commit wie die Auswahl verwendet. Erst wenn außerdem ComfyUI-HTTP antwortet, startet über eine zweite authentifizierte SSH-Verbindung ein **rein lesender** Remote-Audit. Dieser liest den gepinnten `manifest.resolve`, prüft `download_failures.json`, vorhandene Modellgewichte und HF-Snapshots, gepinnte Custom-Node-Verzeichnisse/Provenienz, importierte Workflow-Dateien sowie die ComfyUI-API `/system_stats` und `/object_info`. Nur bei vollständigem Erfolg wird einmalig ein Dialog **„ComfyUI ist einsatzbereit“** gezeigt. Bei fehlenden Modellen, Node-Importen, Paketkonflikten, abweichendem Commit, unklarem Ergebnis oder Verbindungsverlust bleibt die Fertigmeldung aus. **Dies ist keine Inferenz- oder kryptografische Gewichtsprüfung und noch nicht an einem echten Thunder-Pod getestet.**

**Betriebshinweise (v0.1.2, nicht vollständig live auf Thunder verifiziert):** Der Bootstrap wird mit `nohup`/`setsid` als eigenständiger Prozess auf dem Pod gestartet. Das nur für den Benutzer zugängliche Verzeichnis `~/.thunderclient-deployment` speichert eine Run-ID, PID, GitHub-Commit, Workflow-Auswahl und ein fortlaufendes `output.log`, **aber keine Zugangstokens**. Die GUI folgt dem Log über SSH; nach einem Verbindungsabbruch kann eine erneute, ausdrücklich bestätigte Bereitstellung mit derselben Auswahl an den noch laufenden Job anschließen. Schließen der GUI oder „Automatik abbrechen“ beendet **nur die Logverbindung, nicht den kostenpflichtigen Pod oder Remote-Job**. Der SSH-Log-Follower besitzt weiterhin eine 12-Stunden-Grenze und benötigt danach eine neue bestätigte Verbindung. Ein realer Live-E2E-Test des Hintergrundprozessverhaltens bleibt vor einer Produktionsfreigabe erforderlich. Die VM wird bei fehlgeschlagenen Downloads nicht automatisch gelöscht.

Für Civitai-Modelle ist ein hinterlegter Civitai-API-Key Voraussetzung für den automatischen Vollinstallationsmodus. Gated-Modelle und zusätzliche Anbieterfreigaben sind damit nicht garantiert. Tokens werden aus Windows Credential Manager über den bestehenden SSH-Standard-Eingabekanal übergeben, nicht als Kommandozeilenargumente. Das private Workflow-Repository wird nicht verändert.

## Systemstatus und Einrichtungsassistent

Die achte Ansicht **Systemstatus** prüft Voraussetzungen erst nach einem ausdrücklichen Klick auf **„Jetzt alles sicher prüfen“**. Ein Button auf der Übersicht führt direkt dorthin. Jeder Eintrag zeigt Zustand, letzte Prüfung, Diagnose, konkrete Einrichtungsanleitung, einen Link zur offiziellen Anbieterseite und den passenden Bereich der App.

Der Systemstatus unterscheidet ausdrücklich:

- **Erfolgreich getestet:** eine echte, ausschließlich lesende Prüfung war erfolgreich (GitHub-API-Zugriff auf das private Repository, `tnr status`, optionale Hugging-Face-/Civitai-Kontoabfragen oder zuvor vertrauenswürdig konfigurierte SSH-Anmeldung).
- **Vorhanden:** lokale Programme bzw. die angegebene SSH-Schlüsseldatei wurden gefunden; eine Netzwerkanmeldung ist dadurch **nicht** bewiesen.
- **Fehlt / Einrichtung nötig / Fehler:** konkrete Abhilfe und offizieller Einrichtungsweg sind direkt anklickbar.
- **Nicht prüfbar:** insbesondere SSH ohne laufenden Pod bzw. ohne zuvor unabhängig bestätigten Hostfingerprint; optional fehlende Provider-Tokens sind als optional gekennzeichnet.

Die Prüfungen nutzen nur lesende API-Aufrufe. Keine Instanzerstellung, kein Bootstrap, keine Modelldownloads, kein Auto-Trust für SSH-Hostschlüssel und keine automatischen Installationen. Token aus Windows Credential Manager werden ausschließlich als HTTPS-Bearer-Header an den jeweils zuständigen Anbieter gesendet, niemals als URL-Parameter oder in Statuslogs. Für Hugging Face/Civitai bestätigt der erfolgreiche Konto-Test **nicht** den Zugriff auf jedes einzelne Gated-Modell.

## Privater GitHub-Zugriff: kein Checkout

Der Client verwendet für das Workflow-Repository **ausschließlich die authentifizierte GitHub-REST-API über HTTPS**, keinen `git clone`, keinen Sparse Checkout und keinen lokalen Arbeitsbaum. Beim Start sowie über „Katalog aktualisieren“:

1. Einen **Fine-grained Personal Access Token** im Windows Credential Manager lesen. Der Token muss ausschließlich für `thomasasen/runpod-ai-workflows` die Berechtigung **Contents: Read-only** haben.
2. Den `main`-Commit und den zugehörigen Git-Tree über die GitHub-API laden. Einträge unter `incoming/` werden nur als Metadaten empfangen und **nie als Dateien auf Windows ausgecheckt**. Die bekannten Windows-ungültigen Dateinamen sind deshalb unkritisch.
3. Ausschließlich `workflows/*/model.json`, gegebenenfalls die zugehörigen `README.md` und `thunder/bootstrap.sh` über ihre **Blob-SHA** abrufen; die Namen der `workflow*.json`-Dateien stehen im Tree. Workflows und Bootstrap liegen **nur im Arbeitsspeicher**, nicht als Cache auf der Festplatte.
4. Ein unvollständig/ungültig geladener Katalog wird nicht übernommen. Bei Netzwerkausfall kann nur der zuletzt in **derselben Sitzung** geladene Stand nach ausdrücklicher Zustimmung weiterverwendet werden.

Der Token wird nur in HTTPS-Authorization-Headern benutzt, nie als URL-Abfrageparameter, Git-Prozessargument oder Konfigurationsdatei. Die Anwendung lässt für GitHub-API-Requests keine Weiterleitungen zu anderen Hosts zu. Ein PAT ist **nicht** in der App enthalten und muss lokal vom berechtigten Benutzer hinterlegt werden. Die GitHub-Repository-Berechtigungen werden nicht geändert. Die Ausführung des vorhandenen Remote-Bootstraps über SSH braucht denselben Lesetoken.

## Pod-Ports und nachvollziehbare SSH-Fehlerdiagnose

Unter **Instanzen** wird der vom Anbieter gemeldete **SSH-Port** jetzt neben IP und Pod-Status angezeigt. Die ID `0` ist bei Thunder gültig; ein nicht eingetragener Port wird als „Noch nicht bereit“ gekennzeichnet.

Der offizielle Thunder-Befehl `tnr connect 0 --json` liefert erfolgreiche SSH-Verbindungsdetails als JSON und **Fehler als JSON-Objekt auf stdout**. Vorher zeigte der Desktop-Client bei Exitcode 1 nur den stderr-Fortschritt und die generische CLI-Hilfe, wodurch die eigentliche Ursache verborgen blieb. Der Client wertet jetzt vorrangig das strukturierte `error`-Feld aus, entfernt offensichtliche Tokens, beschränkt die Länge und bewahrt den Exitcode. Gleichzeitige CLI-Verbindungsanbahnungen innerhalb eines Client-Prozesses werden serialisiert.

**Wichtig:** `tnr connect --json` ist **nicht garantiert rein lesend**: Die CLI kann einen noch fehlenden SSH-Schlüssel auf dem Thunder-Konto erzeugen und lokal speichern. Die Systemstatus-Ansicht prüft deshalb SSH-Key-Existenz nun **ohne** `tnr connect`; für einen Live-SSH-Test ist ein zuvor verifizierter Hostfingerprint nötig.

Der Remote-SSH-Port wird von Thunder vorgegeben und nicht mit `--disk`/`--gpu` beim Erstellen gewählt. Für ComfyUI verwendet der Client einen privaten Tunnel `127.0.0.1:8188 → Pod:127.0.0.1:8188` über die SSH-Verbindung. Eine öffentliche HTTP-Portfreigabe wird dafür **nicht** benötigt. HTTP-Freigaben, soweit aktiv, können gesondert **lesend** mit `tnr ports list --json` geprüft werden. Keine Portfreigabe erfolgt automatisch.

## SSH-Abbruch „A task was canceled“ (Alpha.15)

Die vorangegangene Alpha konnte bei einer hängenden Windows-OpenSSH-Abfrage nach einem bereits erstellten Pod lediglich **„A task was canceled“** anzeigen. Ein internes Befehl-Zeitlimit war in `ProcessRunner` nicht von einem ausdrücklichen Benutzerabbruch unterscheidbar; Diagnose und Wiederanlauf waren dadurch erschwert.

Ab **v0.1.0-alpha.16** werden zeitbegrenzte CLI-/SSH-Prozesse als **Zeitüberschreitung mit Programmnamen und Sekundenangabe** statt als nichtssagender Abbruch gemeldet. Die Automatik protokolliert die SSH-Vorbereitung Schritt für Schritt (Thunder-Endpunkt, Hostvertrauen, `ssh-keyscan`-Versuch, isolierter `ssh.exe`-Fallback, Fingerprint-Dialog, SSH-Login). Damit ist der tatsächlich blockierende Schritt bei der nächsten Ausführung erkennbar. `ssh-keyscan` wird bei Zeitüberschreitung kontrolliert wiederholt; der `ssh.exe`-Fallback kann einen bereits temporär erfassten öffentlichen Hostschlüssel auch dann verwenden, wenn der `-N`-Handshake erst am Zeitlimit beendet wird. Der echte Hostschlüssel bleibt **nur nach unabhängiger Fingerprint-Prüfung** vertrauenswürdig; `StrictHostKeyChecking=yes` bleibt aktiv.

Eine **vorhandene** VM soll unter **Instanzen** geprüft und nach ausdrücklicher Zustimmung weiterverwendet werden. Keine automatische zweite GPU-VM und kein automatisches Löschen bei Fehler. Der Pod kann weiter Kosten verursachen; die Modelldownloads starten erst nach einem erfolgreichen SSH-Login.

## Thunder-API-Timeout beim Einrichten des SSH-Schlüssels (Alpha.16)

Wenn der Pod bereits `RUNNING` ist, `tnr connect --json` aber mit `failed to add SSH key ... POST /v1/instances/<id>/add_key: context deadline exceeded` endet, ist **nicht der SSH-Port** gescheitert, sondern die Anfrage an den Thunder-API-Endpunkt. Die offizielle CLI verwendet für HTTP-Anfragen ein **30-Sekunden-Limit**; das Ergebnis einer erfolglos beantworteten POST-Anfrage ist **unklar**. Sie könnte serverseitig bereits bearbeitet worden sein, auch wenn die lokale private SSH-Key-Datei noch nicht gespeichert wurde.

Ab `v0.1.0-alpha.16` erkennt der Client dieses Fehlerbild ausdrücklich, beendet den Automatismus **ohne stillschweigende Wiederholung von `add_key` oder Pod-Erstellung** und informiert über die weiterhin kostenpflichtige Instanz. Ein weiterer Versuch mit dem bestehenden Pod ist **nur nach erneuter ausdrücklicher Bestätigung** möglich; dabei könnte die offizielle CLI einen weiteren SSH-Schlüssel registrieren. Eine beliebige Erhöhung des HTTP-Timeouts löst keine bekannte serverseitige Störung und keinen unklaren POST-Zustand zuverlässig.

In **Downloads & Logs** stehen direkte Buttons zur offiziellen [Thunder-Konsole](https://console.thundercompute.com/) und zum [Dienststatus](https://status.thundercompute.com/). Bei anhaltendem Fehler den Pod unter **Instanzen** bewusst prüfen oder (nach Backup-/Snapshot-Prüfung) löschen und das kopierbare, bereinigte Protokoll an den [Thunder-Support](https://www.thundercompute.com/contact) geben. Ist der allgemeine Dienststatus grün, kann dennoch der einzelne `add_key`-Endpunkt langsam oder gestört sein. **Modelldownloads starten erst nach erfolgreichem SSH-Login.**

## Verifizierter SSH-Erstzugang über dokumentierten Public-Key-Modus (Entwicklungsbranch 16.11.2026)

**Ursache des mehrfach reproduzierten P0:** `tnr connect --json` sendet auf dem ersten Pod ohne lokale Key-Datei ein `POST /v1/instances/{id}/add_key` **ohne** `public_key`. Laut offizieller Thunder-API erzeugt das einen neuen SSH-Privatschlüssel, der **nur in der ersten API-Antwort** übertragen wird. Wenn diese Antwort innerhalb von 30 Sekunden ausbleibt, kann der Schlüssel im Pod liegen, ohne dass ihn Windows besitzt. Erneutes `tnr connect` kann dann ein weiteres Schlüsselpaar erzeugen. Dieser unsichere Pfad wird im GUI-Client **nicht mehr aufgerufen**.

**Neue Route:** Der Client verwendet einen **benannten Thunder-API-Token** aus dem Windows Credential Manager und einen **lokal persistierten ED25519-Privatschlüssel**. Er sendet immer den **abgeleiteten öffentlichen Teil** als JSON-Feld `public_key` an den dokumentierten Endpunkt. Thunder garantiert für wiederholte identische öffentliche Schlüssel No-op-Semantik. Auf 5xx oder Zeitüberschreitung sind maximal zwei Versuche **mit exakt demselben Public-Key** möglich. Bei weiterhin unklarer Antwort prüft der Client nach der unabhängigen Hostfingerprint-Bestätigung, ob der Schlüssel bereits per SSH funktioniert. Kein Rückfall auf serverseitige Keygenerierung, kein Zweit-Pod und keine öffentliche ComfyUI-Freigabe.

**Einrichtung für einen bestehenden Pod (ohne neue GPU-Miete):**

1. In der [Thunder Console](https://console.thundercompute.com/) unter **Authentication / API Tokens** einen eigenen benannten API-Token anlegen. Für `tnr` außerdem unter Windows mit dem **gleichen Thunder-Account/Workspace** per `tnr login` angemeldet bleiben.
2. In der Windows-App **Zugangsdaten → Thunder · API-Token für sicheren SSH-Erstzugang** ausfüllen und **Thunder-API-Token speichern** wählen. Nicht den GitHub-Token verwenden. Der API-Token liegt im Windows Credential Manager und wird nur als HTTPS-Bearer-Header gesendet.
3. Unter **Systemstatus** den Check **Thunder Compute · eigener API-Token (SSH-Erstzugang)** ausführen: Er macht nur `GET /v1/instances/list`; **kein Create, kein Add-Key, kein Delete**. OpenSSH, CLI-Login und GitHub ebenfalls überprüfen.
4. Unter **Instanzen** den *bereits existierenden* Pod wählen und **Bestehenden Pod automatisch einrichten** bestätigen. Der Client prüft den vorhandenen privaten Schlüssel oder erzeugt lokal einmalig `%LOCALAPPDATA%\ThunderClient\ssh\identity_ed25519`. Der private Schlüssel verlässt diesen Windows-PC nicht. Nur `ssh-keygen -y`-abgeleitetes `ssh-ed25519` wird registriert.
5. Den neu ermittelten **ED25519-Hostfingerprint zwingend gegen eine unabhängige Thunder-Quelle abgleichen**. Ohne explizite Bestätigung bricht die Einrichtung ab. Anschließend wird mit dem fest gespeicherten privaten Schlüssel ein echter `ssh ubuntu@<IP> -p <Port>`-Authentifizierungstest ausgeführt. Erst danach folgen GPU-/Speicherprüfung, Bootstrap, parallele Downloads und Remote-Fertigkeits-Audit.

**Vor jedem kostenpflichtigen Auto-Create** werden der API-Token (read-only Provider-Abfrage), der lokale ED25519-Schlüssel und die GPU-Kostenschätzung geprüft. Ist der API-Token nicht verfügbar und liegt kein bereits autorisierter lokaler Schlüssel vor, wird **kein neuer Pod erstellt**. Die reine manuelle Instanzerstellung ist eine separate, bewusst kostenpflichtige Funktion.

**Einschränkungen und Eskalation:** Wenn die Thunder-API auch für `public_key` nicht antwortet **und** der anschließende SSH-Probe mit dem vorhandenen Schlüssel scheitert, ist das Provider-Zugangsproblem nicht lokal gelöst. Ein HTTP-200 vom Add-Key-Endpunkt gilt **nicht** als erfolgreicher SSH-Login. `Permission denied (publickey)`, abweichende UUID, HTTP 401/403, nicht bestätigter Hostfingerprint und fehlende Modell-/Node-Registrierung bleiben Sperrgründe. Der Client hat für diesen neuen Pfad Windows-/Linux-CI und Mocktests, aber **noch keinen bestätigten erfolgreichen Live-SSH-Login mit dem betroffenen Thunder-Pod**. Es ist weiterhin keine Produktionsfreigabe. Details: [Red-Team-P0-Analyse](docs/THUNDER_SSH_P0_REDTEAM_2026-11-16.md).

**Primärdokumentation:** [Thunder API: Add SSH key to instance](https://www.thundercompute.com/docs/api-reference/instances/add-ssh-key-to-instance), [Thunder API: List instances](https://www.thundercompute.com/docs/api-reference/instances/list-instances), [Thunder Console: SSH manually](https://www.thundercompute.com/docs/console/operations/ssh), [Thunder Console: Authentication](https://www.thundercompute.com/docs/console/operations/authentication), [Thunder-CLI Quellcode](https://github.com/Thunder-Compute/thunder-cli/blob/main/cmd/connect.go).

## Bereits autorisierten SSH-Schlüssel verwenden (Entwicklungsbranch, nach Thunder-API-Timeout)

Wenn `tnr connect 0 --json` beim serverseitigen `POST /v1/instances/{id}/add_key` nach 30 Sekunden abbricht, **keinen weiteren Pod erstellen**. Die Erstellung kann bereits kostenpflichtig sein, und der fehlgeschlagene POST kann trotzdem bereits einen Schlüssel registriert haben. Das Clientupdate prüft jetzt zuerst **rein lesend** `TNR_HOME/keys/<pod-uuid>` (standardmäßig `%USERPROFILE%\.thunder\keys\<pod-uuid>`). Falls diese *private* Schlüsseldatei bereits existiert, verwendet der Client die direkt aus `tnr status` gemeldete IP und den SSH-Port und **ruft `tnr connect` und `/add_key` nicht auf**. Der bestehende ED25519-Hostfingerprint muss weiterhin unabhängig bestätigt werden; anschließend erfolgt der normale nichtinteraktive SSH-Authentifizierungstest.

**Sicherheitsrelevanter Unterschied laut [offizieller API-Dokumentation](https://www.thundercompute.com/docs/api-reference/instances/add-ssh-key-to-instance):** Beim API-`POST /v1/instances/{id}/add_key` **ohne** `public_key` erzeugt der Anbieter ein neues Schlüsselpaar und liefert den privaten Schlüssel nur einmal zurück. **Mit ausdrücklich angegebenem `public_key`** wird der vorhandene öffentliche Schlüssel auf dem Pod eingetragen; ein erneutes Anhängen desselben Keys ist laut Thunder ein No-op. Für Wiederherstellung nur den ausdrücklich gewählten Public-Key-Modus benutzen, nicht den unklaren CLI-Neu-Generierungsmodus wiederholen.

Für einen von Thunder **bereits für genau diesen Pod autorisierten** eigenen SSH-Schlüssel gibt es einen opt-in Read-only-Weg. Der offizielle Thunder-Console-SSH-Leitfaden dokumentiert das Speichern öffentlicher Keys unter [Authentication → SSH Keys (Advanced)](https://console.thundercompute.com/settings/ssh), die Zuordnung bei der Pod-Erstellung und für **bestehende** Pods das öffentliche [Add SSH key to instance](https://www.thundercompute.com/docs/api-reference/instances/add-ssh-key-to-instance)-API-Verfahren. **Das bloße Speichern eines Org-Keys ordnet ihn einem bereits laufenden Pod nicht automatisch zu.** Der Providerzugriff muss zuerst erfolgreich abgeschlossen sein.

1. Eigenes Schlüsselpaar sicher unter Windows erzeugen, falls keines existiert: `ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\thunderclient-ed25519"` (vorhandene Schlüssel **nicht** überschreiben). Nur die Datei mit dem Suffix `.pub` bei Thunder als **öffentlichen** Schlüssel hinterlegen und nach dem dokumentierten Verfahren dem **bestehenden** Pod zuordnen. Den privaten Key niemals an Thunder oder den Support senden.
2. Den Pfad zur vorhandenen **privaten**, bereits auf dem Pod zugelassenen Datei in einer PowerShell eintragen: `[Environment]::SetEnvironmentVariable("THUNDERCLIENT_SSH_KEY", "$env:USERPROFILE\.ssh\thunderclient-ed25519", "User")`. Den Desktop-Client danach vollständig schließen und erneut starten, damit er die Windows-Umgebungsvariable sieht.
3. Unter **Instanzen** denselben bereits laufenden Pod auswählen und ausdrücklich **Bestehenden Pod automatisch einrichten** wählen. Die App verwendet den privaten Schlüssel mit dem Anbieter-SSH-Endpunkt **ohne** den nicht-idempotenten Thunder-`/add_key`-POST. Bei fehlendem oder ungültigem Schlüsselpfad wird die Einrichtung **gesperrt**, statt unerwartet einen neuen Schlüssel zu registrieren. Vor der ersten Anmeldung muss der angezeigte ED25519-Hostfingerprint unabhängig mit der vertrauenswürdigen Anbieterquelle übereinstimmen.

Der gewählte Schlüssel muss tatsächlich in den `authorized_keys` des laufenden Pods stehen; ein lokaler Schlüssel allein beweist das nicht. Scheitert `Permission denied (publickey)`, zuerst die **bestehende** Schlüsselzuordnung beim Anbieter prüfen. Die neue Funktion repariert **keinen** serverseitig blockierten `add_key`-Dienst. Wenn kein autorisierter privater Schlüssel existiert und `THUNDERCLIENT_SSH_KEY` nicht gesetzt ist, kann ein ausdrücklich bestätigter Standard-Verbindungsversuch weiterhin den API-POST auslösen. Die Desktop-App hat keinen Zugang zu deinem lokalen Windows-Dateisystem, solange sie nicht auf deinem PC gestartet wird.

## Windows-SSH-Hostschlüssel: `choose_kex: unsupported KEX method`

Ab **v0.1.0-alpha.16** wird der bekannte Fehler in **Windows `ssh-keyscan.exe`** mit `sntrup761x25519-sha512@openssh.com` (Win32-OpenSSH [#2140](https://github.com/PowerShell/Win32-OpenSSH/issues/2140)) automatisch behandelt: `ssh.exe` holt den *öffentlichen ED25519-Hostschlüssel* über einen einmaligen, **nicht authentifizierten** Handshake in eine **isolierte, direkt danach gelöschte Datei**. Der eigene, dauerhaft vertrauenswürdige `known_hosts`-Speicher wird **nicht** geändert, bevor der Benutzer den Fingerprint mit einer unabhängigen vertrauenswürdigen Quelle verglichen und ausdrücklich bestätigt hat. Es werden keine privaten Schlüssel oder Remote-Kommandos für diese temporäre Abfrage verwendet. Alle späteren SSH-Aktionen behalten `StrictHostKeyChecking=yes`.

Bei Abbruch bleibt der bereits erstellte Thunder-Pod bestehen und kann Kosten verursachen. Kein automatischer zweiter Create-Versuch. Unter **Instanzen** den bestehenden Pod verwalten; beim nächsten bestätigten Ein-Klick-Start bietet der Client die Wiederverwendung der einzigen vorhandenen Instanz an. **Nicht** wegen dieses Fehlers einen neuen Pod erstellen. ComfyUI-Downloads starten erst *nach* erfolgreicher SSH-Hostvertrauensprüfung.

## SSH-Hostschlüssel und Verbindungsdiagnose

Beim erstmaligen Verbinden prüft der Client einen **ED25519-Hostschlüssel** des Thunder-SSH-Endpunkts. Ab `alpha.10` werden vorübergehende fehlgeschlagene `ssh-keyscan`-Abfragen begrenzt wiederholt; die Ausgabe enthält bei einem Fehlschlag Host, Port, Exitcode und die relevante Diagnose des Windows-OpenSSH-Clients. Wenn vorhanden, verwendet die App bewusst den nativen Windows-OpenSSH-Client unter `System32\\OpenSSH`.

Der Client akzeptiert **keinen** neuen Schlüssel automatisch. Der SHA256-Fingerprint muss anhand einer **unabhängigen, vertrauenswürdigen Thunder-Quelle** bestätigt werden. `StrictHostKeyChecking=yes` bleibt aktiviert. Falls der SSH-Dienst nach dem Pod-Start noch nicht erreichbar ist, später erneut versuchen; diese Anpassung garantiert keine SSH-Verbindung bei gesperrtem/falschem Port oder fehlendem ED25519-Schlüssel.

Fehlersuche: [Troubleshooting](docs/TROUBLESHOOTING.md).

## Voraussetzungen

- Windows 11 x64 (Windows 10 nicht validiert).
- Offizielle Thunder-Compute-CLI `tnr` mit `tnr login`. Die GUI findet `tnr.exe` über PATH bzw. die Standardverzeichnisse unter Program Files.
- OpenSSH-Client, `ssh-keygen` und `ssh-keyscan`.
- GitHub-Fine-grained-PAT mit **Contents: Read-only** für das private Workflow-Repository, im Tab **Zugangsdaten** in WinCred hinterlegt. Optional HF-/Civitai-Token.
- Vertrauenswürdige, externe Quelle zum Abgleich des SSH-ED25519-Hostfingerprints vor dem ersten Verbindungsaufbau.

**Git for Windows/Git Credential Manager sind für den GUI-Workflow-Katalog nicht mehr erforderlich.** Für andere Projekte können sie natürlich weiterhin installiert bleiben.

## Geführter Ablauf: Katalog → Workflows → Hardware → Pod

Auf der Übersicht startet **„Katalog laden und Workflows wählen“** den lesenden Zugriff auf das private GitHub-Repository und öffnet danach die Workflow-Bibliothek. Anschließend:

1. **Workflow-Auswahl:** Einen oder mehrere Workflows in der Auswahlspalte aktivieren. Bei widersprüchlichen Modelldateien wird die Weiterführung blockiert. **„Auswahl übernehmen → Hardware prüfen“** speichert die Auswahl und öffnet die Instanzkonfiguration.
2. **Hardwareempfehlungen:** Der Client liest die Hardwaretabellen und das RunPod-Speichervolumen aus den authentifizierten Workflow-READMEs. Er dedupliziert Modellgrößen und bildet eine **vorsichtige Thunder-Disk-Planung (Modellgrößen + 40 GB Pauschalreserve, mindestens die höhere dokumentierte RunPod-Referenz)**. GPU-VRAM wird als kleinste in der jeweiligen README-Tabelle gelistete RunPod-VRAM-Klasse ausgewiesen, bei Mehrfachauswahl als größte dieser Klassen. **Das sind Referenzwerte, keine auf Thunder gemessenen Mindestanforderungen.**
3. **Anpassen / Übernehmen:** „Planungswerte übernehmen“ erhöht nur eine zu kleine Disk und aktiviert ausschließlich GPU-Kandidaten, die bei vollständigen README-Angaben zur VRAM-Orientierung passen. Das ist **keine Kompatibilitäts-, Preis- oder Verfügbarkeitsbestätigung**. GPU-Typ, vCPUs, Anzahl, Template und maximalen Schätzpreis ausdrücklich selbst prüfen und ändern.
4. **Pod erstellen und verwalten:** „Pod erstellen“ verlangt weiter die ausdrückliche Kostenfreigabe. Bei Erfolg wird automatisch die Seite **Pods verwalten** geöffnet. Dort sind Status, SSH, privater ComfyUI-Zugang, Installationslogs, Snapshots und bestätigtes Löschen erreichbar.

Bestehende Pods können auch ohne Neuanlage jederzeit über die Seitenleiste geöffnet werden. Ein Stoppen der GPU ohne Löschen ist **nicht** als kostenfreier Pausenmodus implementiert.

## Optimierte Instanzkonfiguration

Unter **Neue Instanz** sind GPU-Priorität, Ressourcen und Startvorschau jetzt in klar getrennten Karten angeordnet. Alle GPU-Zeilen besitzen ein einheitliches Raster mit ausreichend breiten Feldern für vCPUs und geschätzte GPU-Stundenpreise. Ressourcenfelder nutzen ein zweispaltiges Formular mit vollständigen Beschriftungen.

Die kostenpflichtige **Instanz erstellen**-Aktion bleibt beim Scrollen am unteren Rand sichtbar; die separate Sicherheitsbestätigung vor dem Erstellen bleibt unverändert. Der helle und dunkle Modus verwenden die korrekte Schriftfarbe auf primären Aktionsbuttons. Die Windows-GUI-Selbstprüfung kontrolliert beide Fensterbreiten (Standard und unterstütztes Minimum), alle sieben Seiten und den sichtbaren Erstellen-Button.

## Vorhandenen Pod: Software neu starten und Git-Änderungen übernehmen

Unter **Instanzen** oder **Downloads & Logs** steht **„Pod-Software neu starten & Git aktualisieren …“**. Das ist **kein VM-/Betriebssystem-Reboot**: Die Anwendung verwendet denselben RUNNING-Pod, lädt über die authentifizierte GitHub-API eine **frische `main`-Revision** des privaten `runpod-ai-workflows`-Repositories, prüft die bisherigen Workflow-Namen und Modell-Zielkonflikte und pinnt den Thunder-Adapter samt Entrypoint an die neue Commit-SHA. Ohne GitHub-Zugriff, gültige Revision, eindeutige Instanzidentität oder bestätigten SSH-Zugang bleibt der laufende Dienst unverändert. Der Neustart verwendet ausschließlich einen **bereits autorisierten lokalen SSH-Schlüssel**, bestätigt dessen Hostfingerprint und prüft einen echten SSH-Login; er ruft **weder `tnr connect` noch `/add_key`** auf. Falls noch kein Schlüssel für diesen Pod freigegeben ist, muss er zuvor über die separate, ausdrücklich bestätigte Einrichtung registriert werden. Vor dem Stoppen ist eine ausdrückliche Bestätigung erforderlich.

Der Remote-Supervisor beendet ausschließlich seine eigene, durch Run-ID und Prozessgruppe verifizierte Bootstrap-/ComfyUI-Sitzung. Er schreibt die neue Version als separaten, credentials-freien Worker und startet sie **auf demselben Pod**. Ein unbekannter fremder ComfyUI-Prozess wird nicht zwangsweise beendet. Bei uneindeutiger Prozessidentität oder einer nicht innerhalb von 45 Sekunden endenden Prozessgruppe bricht der Neustart ab. Es gibt **keinen Pod-Create, kein Delete, keinen OS-Reboot, kein automatisches Kill -9**.

Der persistente Pfad `~/comfyui-workspace` einschließlich ComfyUI-Modellen, Outputs, Quell-Snapshots und weiteren Daten bleibt unangetastet. Das Upstream-Bootstrap prüft bereits vorhandene Modelle anhand der gespeicherten Dateiprovenienz und Fingerprints und überspringt passende Downloads. **Neue, geänderte, defekte oder nicht verifizierbare Modelle** können erneut heruntergeladen werden; ein generelles Verbot aller Downloads wäre mit aktualisierten Manifesten nicht korrekt. Das Python-venv kann je nach Thunder-Image und Upstream-Startlogik temporär statt persistent liegen; dessen Wiederverwendung ist nicht garantiert.

Der Button bestätigt zunächst nur, dass der neue Remote-Job gestartet wurde. **ComfyUI-/HTTP-Bereitschaft und Modell-Vollständigkeit sind erst nach separater Prüfung bestätigt.** Die laufenden Remote-Logs sind anschließend über **„Bootstrap manuell starten …“** erreichbar, das sich bei identischer Revision/Workflow-Auswahl an den bereits aktiven Job anhängt. GPU-Abrechnung läuft weiter. Für ein vollständiges VM-Recovery dient weiterhin der ausdrücklich bestätigte Snapshot-Weg.

## Live-Tarif, Rechnung und Thunder-Kontoguthaben

Unter **Instanzen → Aktuelle Pod-Kosten & Kontostand → Live-Tarif abrufen** ermittelt der Client über `tnr status --json --no-wait` die tatsächlich gewählten Ressourcen des **laufenden** Pods: GPU-Typ/-Anzahl, `cpuCores` und Instanzdisk. Ausschließlich die offiziellen öffentlichen `GET /v2/pricing` und `GET /v2/specs` liefern die aktuell angebotenen Stundentarife. Der Client zeigt den Tarif und die Einzelkomponenten GPU, zusätzliche vCPUs und Zusatzdisk (USD/h). Fehlende Instanzdaten oder nicht übereinstimmende Anbieter-Spezifikationen führen zu einer klaren Fehlermeldung, **niemals zu einer erfundenen Zahl aus alten Konfigurationswerten**.

Eine zusätzliche Anzeige **„Kosten seit Erstellung (nicht abgerechnet)“** verwendet den gerade abrufbaren Stundentarif und nur bei RUNNING-Status den Zeitraum seit `createdAt`. **Sie ist ausdrücklich kein genauer historischer Rechnungsbetrag**: Änderungen an Hardware, Tarifen, Kostenrabatte und frühere Laufzeitunterbrechungen sind aus dem Status nicht rekonstruierbar; vergangene Preise dürfen nicht als aktuelle Rechnung ausgegeben werden. Der tatsächliche Rechnungsbetrag muss aus Thunders Billing-Daten stammen.

**Balance/Guthaben:** Die dokumentierte Thunder-CLI sowie die bisher eingesetzte REST-API liefern keinen bestätigten Kontoguthaben-Endpunkt. Der Client zeigt deshalb **keine fiktive Balance** und verlinkt auf die offizielle, authentifizierte [Thunder-Abrechnung](https://console.thundercompute.com/settings/billing) zur Live-Prüfung. Dort sind Prepaid-Guthaben und Zahlungsverfahren beim Anbieter verwaltet. Thunder bietet eigene MCP-Billing-Tools (`get_meter_data`, `get_upcoming_invoice`, `get_invoice_history`, `get_subscription`) über eine separate OAuth-Anbindung. Diese ist **nicht** Teil des aktuellen Windows-Clients; ihre Felder bzw. ein Balance-Wert werden nicht als dokumentiertes REST-API-Schema unterstellt. Snapshots werden separat abgerechnet und gehören nicht zum GPU-Stundentarif.

**App-Icon:** Das Blitz-Icon liegt als `src/ThunderClient/Assets/ThunderClient.ico` vor und ist als Executable-`ApplicationIcon`, Windows-Fenster-Icon und Sidebar-Bild eingebunden; Installer und Startmenüverknüpfungen referenzieren die EXE.

## Bedienung

1. App starten und unter **Zugangsdaten** den privaten GitHub-Lesetoken speichern, dann **Katalog aktualisieren**. Ohne Token bleibt die GUI offen und zeigt eine aussagekräftige Fehlermeldung.
2. Unter **Workflows** Einträge wählen. Einmalig geladene Metadaten werden in der laufenden Sitzung gehalten; eine neue Sitzung lädt die Daten erneut.
3. Unter **Neue Instanz** GPU-Reihenfolge, vCPUs, Disk, Template und geschätzte Stundengrenze prüfen. Die erste kostenpflichtige Erstellung braucht immer eine ausdrückliche Bestätigung.
4. Unter **Instanzen** Status/Snapshots abrufen, SSH-Fingerprint unabhängig verifizieren und dann über **Downloads & Logs** den Bootstrap starten.
5. ComfyUI über den **privaten SSH-Loopback-Tunnel** öffnen; vor dem Löschen fertiggestellte Sicherungen prüfen.

In **Downloads & Logs** ist das Protokoll schreibgeschützt, markierbar und mit Strg+C kopierbar. Die GUI zeigt die letzten 600 Zeilen performant an. Der separate **Logexport** umfasst bis zu 100.000 bereinigte Zeilen der aktuellen Windows-Sitzung; bei Überschreitung weist er auf die Begrenzung hin. Das vollständige Bootstrap-Log der aktuellen oder eines früheren Remote-Laufs bleibt im privaten Pod-Verzeichnis abrufbar. „Log kopieren“ und „Startdiagnose kopieren“ verwenden die Windows-Zwischenablage; private Informationen vor dem Teilen prüfen.

## Lokaler Build

```powershell
dotnet restore tests/ThunderClient.Tests/ThunderClient.Tests.csproj
dotnet test tests/ThunderClient.Tests/ThunderClient.Tests.csproj -c Release
dotnet publish src/ThunderClient/ThunderClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish
```

Der Windows-Installer wird in GitHub Actions erstellt; er ist noch nicht codesigniert. Die App installiert Releases nicht automatisch. **Der kostenpflichtige Neuerstellungsablauf verwendet zusätzlich eine Read-only-Livequote aus der Thunder-API; eine serverseitige Kostensperre entsteht dadurch nicht.** Laufkosten bestehender Pods bleiben Schätzwerte. Der Benutzer ist selbst für die Kontrolle kostenpflichtiger Ressourcen verantwortlich.

Siehe [Benutzerhandbuch](docs/USER_GUIDE.md), [Architektur](docs/ARCHITECTURE.md), [Sicherheitsgrenzen](docs/SECURITY_REVIEW.md), [Fehlerdiagnose](docs/TROUBLESHOOTING.md) und [Implementierungsstand](docs/IMPLEMENTATION_STATUS.md).
