## Ein-Klick-Start: SSH-Zeitlimit richtig erkennen (Alpha.15)

Wenn nach erfolgreicher Pod-Erstellung „A task was canceled“ angezeigt wurde, handelt es sich **nicht automatisch um einen Nutzerabbruch**. In der neuesten Alpha stehen für die SSH-Vorbereitung konkrete Schritte im Protokoll und interne Zeitlimits werden mit dem Programmnamen und den Sekunden gemeldet. **Denselben laufenden Pod verwenden, keinen zweiten Pod anlegen.** Unter **Instanzen** den vorhandenen Pod überprüfen und mit „Bestehenden Pod automatisch einrichten“ nach Bestätigung erneut versuchen. Der Hostfingerprint muss weiterhin unabhängig geprüft werden. Bei erneuten Problemen das kopierbare Log (ohne Geheimnisse) verwenden. Die kostenpflichtige Instanz läuft möglicherweise bis zur ausdrücklichen Löschung weiter.

## Bekannter Windows-SSH-Fehler automatisch abgefangen

Wenn ein Thunder-Pod schon erstellt wurde, die Verbindung aber mit `choose_kex: unsupported KEX method sntrup761x25519-sha512@openssh.com` abbricht, **keinen zweiten Pod anlegen**. Das ist ein Windows-`ssh-keyscan`-Problem. Ab **alpha.14** übernimmt die App die Hostkey-Abfrage über den normalen SSH-Client. Sie verlangt weiterhin deinen Vergleich und die ausdrückliche Bestätigung des Fingerprints aus einer unabhängigen vertrauenswürdigen Quelle. Ist ein Pod vorhanden, kann dieser während der Fehlersuche weiter Kosten verursachen. Unter **Instanzen** den bestehenden Pod auswählen und seine Kosten bzw. seine bewusste Löschung prüfen. Für die erneute Installation **„Bestehenden Pod automatisch einrichten“** verwenden, nicht „Nur Pod erstellen“.

# Benutzerhandbuch

## Automatische VM-Erstellung und Modelldownloads

1. Unter **Systemstatus** GitHub-/Thunder-/OpenSSH-Einrichtung prüfen und bei Bedarf Civitai/HF-Tokens unter **Zugangsdaten** hinterlegen.
2. **Workflows** öffnen, gewünschte Ordner markieren und **„Automatisch bereitstellen: Pod + ComfyUI + Modelle …“** anklicken. Die Hardware wird aus dem Manifestbudget und den RunPod-README-Hardwareklassen geplant, aber nicht als Thunder-Kompatibilität ausgegeben. Alternativ vor dem Klick unter **Neue Instanz** GPU und Disk anpassen.
3. Das Dialogfenster mit deduplizierter Modellgröße, GPU-Fallback, vCPUs, Gesamtdisk, Preisschätzung und Warnungen kontrollieren. **Erst nach Ja** erfolgt der kostenpflichtige Start; die Preisgrenze ist eine lokale Schätzung, keine Thunder-Abrechnungssperre. Existiert ein Pod, wird er nach Zustimmung verwendet. Es wird **kein zweiter Pod** angelegt.
4. Der Client wartet automatisch auf `RUNNING` und einen gültigen SSH-Endpunkt. `tnr connect --json` kann einen fehlenden SSH-Schlüssel erzeugen. **Neue Hostfingerprints unbedingt mit einer unabhängigen Thunder-Quelle vergleichen** und nur bei Übereinstimmung bestätigen.
5. Nach erfolgreichem SSH-Login startet automatisch das vorhandene `thunder/bootstrap.sh`. Modell-/Node-Transfers laufen bereits während der ComfyUI-Einrichtung an. Das Upstream-Verfahren nutzt maximal **vier adaptive parallele Datei-Jobs**; die Echtzeitereignisse erscheinen unter **Downloads & Logs**. Der Upstream meldet HTTP-Bereitschaft an `127.0.0.1:8188/system_stats`. Zum Browserzugriff **Instanzen → ComfyUI öffnen (SSH)** nutzen.

**Die Desktop-Anwendung während dieser ersten Ein-Klick-Installation geöffnet lassen.** Das Remote-ComfyUI läuft nach erfolgreichem Bootstrap im SSH-Prozess; ein unterbrochener SSH-Stream ist keine verlässliche Erfolgsmeldung. Das Programm erzeugt nach Abbruch oder SSH-Fehler **keinen** weiteren Pod und startet die Installation nicht blind erneut. **Automatik abbrechen** unterbricht nur die laufende Verbindung; ein vorhandener Pod verursacht möglicherweise weiterhin GPU-Kosten. Bestehende Outputs vor Löschung per Snapshot sichern. Der Modus erkennt ein bereits laufendes ComfyUI und überspringt dann einen zweiten Bootstrap, bestätigt aber **nicht**, dass alle neu ausgewählten Workflows installiert sind.

Bei Civitai-Modellen verlangt der automatische Modus vorab einen gespeicherten Civitai-API-Key. Fehlen Größenangaben, eine aktive GPU innerhalb der Preisgrenze oder explizite Manifestautorisierungen, wird vor dem kostenpflichtigen Start abgebrochen.

## SSH-Ports und Schlüsselkontrolle

In **Instanzen** zeigt die Liste jetzt den tatsächlichen, von Thunder gemeldeten SSH-Port. Der Port kann vor Fertigstellung des Pod-Starts noch fehlen. Die numerische Instanz-ID `0` ist normal.

Es gibt zwei unterschiedliche Portarten: der von Thunder zugewiesene SSH-Port für `ubuntu@<pod-ip>` und der **lokale** ComfyUI-Loopback-Port `127.0.0.1:8188`. Der Client leitet den ComfyUI-Port ausschließlich durch den SSH-Tunnel weiter. Er veröffentlicht dadurch keinen ComfyUI-Port über eine öffentliche HTTP-URL. Bereits vorhandene öffentliche Portfreigaben werden mit `tnr ports list --json` (nur lesend) ermittelt; der Client legt sie nicht automatisch an.

Bei Problemen mit `tnr connect --json` ist die von der CLI ausgegebene strukturierte JSON-Fehlermeldung entscheidend; Fortschritt und `Usage:` sind dafür nicht ausreichend. Eine bewusste SSH-Verbindung kann einen fehlenden SSH-Schlüssel im Thunder-Konto generieren. Der **Systemstatus** prüft dagegen nur lokale Schlüsseldateien unter dem Standard-TNR-Verzeichnis (oder `TNR_HOME`) und ein bereits bestätigtes SSH-Hostvertrauen, ohne Schlüssel zu erzeugen. Alle Fingerprints müssen weiterhin unabhängig bestätigt werden.

## Systemstatus und Einrichtung

In der Seitenleiste **Systemstatus** öffnen oder auf der Übersicht **Systemstatus prüfen** anklicken. Die Anzeige startet mit **Nicht geprüft**; ein gespeicherter Token allein ist kein erfolgreicher Verbindungstest. **Jetzt alles sicher prüfen** führt auf ausdrücklichen Wunsch nur lesende Prüfungen aus. Zu jedem der sieben Punkte werden Ergebnisse, Zeitstempel, ein direkter offizieller Einrichtungslink und eine Abkürzung in den jeweiligen App-Bereich angezeigt.

- **GitHub:** „GitHub-Token erstellen“ führt direkt zum Fine-grained-PAT-Formular. Nur das private `thomasasen/runpod-ai-workflows` freigeben, **Contents: Read-only** wählen, Token unter **Zugangsdaten** speichern. Als getestet gilt erst der vollständige authentifizierte Abruf einer privaten GitHub-Katalogrevision.
- **Thunder-CLI:** Den offiziellen MSI-Installer herunterladen, `tnr login` in PowerShell ausführen und den Check erneut starten. Eine erfolgreiche `tnr status --json --no-wait`-Antwort bestätigt die CLI-Anmeldung, nicht den Pod-Betrieb.
- **OpenSSH:** Benötigt werden `ssh.exe`, `ssh-keyscan.exe`, `ssh-keygen.exe`. Der Link öffnet Microsofts Anleitung für den Windows-OpenSSH-Client. Ein OpenSSH-**Server** muss auf dem PC nicht installiert werden.
- **SSH-Schlüssel:** Erst mit einem vorhandenen laufenden Pod kann der Client die von Thunder genannte private Schlüsseldatei prüfen. Er liest dabei nie den privaten Schlüsselinhalt.
- **SSH-Anmeldung:** Der Hostfingerprint muss unabhängig über die Thunder-Konsole geprüft und im normalen Verbindungsdialog bestätigt sein. Nur danach testet der Systemstatus einen harmlosen SSH-`true`-Befehl. Hostprüfung wird nie automatisch abgeschaltet oder übersprungen.
- **Hugging Face und Civitai:** Optional. Die Links führen zu den jeweiligen Token-Verwaltungsseiten. Sind Tokens hinterlegt, prüft ein lesender Konto-Endpunkt die API-Anmeldung. Das testet **nicht** jedes private, zugangsbeschränkte oder kostenpflichtige Modell.

Fehlende optionale Tokens werden nicht als zwingender Fehler gewertet. Keine Prüfung erstellt oder startet einen Pod. Der Check kann ein bereits laufendes, kostenpflichtiges Pod verwenden, erzeugt dadurch aber keine zusätzliche Instanz.

## Einrichtung für das private Repository

1. Die offizielle Thunder-Compute-CLI von https://github.com/Thunder-Compute/thunder-cli/releases/latest installieren und mit `tnr login` anmelden.
2. Im eigenen GitHub-Konto einen **Fine-grained Personal Access Token** erstellen, den Zugriff auf das private Repository `thomasasen/runpod-ai-workflows` beschränken und **Repository permissions → Contents: Read-only** setzen. Der Benutzer muss hierfür auf das private Repository zugreifen dürfen.
3. Im Client unter **Zugangsdaten** den Token im Windows Credential Manager speichern. Das Feld ist maskiert. Der Client benötigt den Token sowohl zum privaten Katalogabruf als auch für den bestehenden Remote-Bootstrap; **Git for Windows ist nicht erforderlich**.
4. OpenSSH-Client, `ssh-keygen`, `ssh-keyscan` bereitstellen. Für HF/Civitai gegebenenfalls separate Tokens hinterlegen.
5. Unter **Übersicht → Katalog aktualisieren** den privaten Katalog aus GitHub laden. Beim App-Start erfolgt ebenfalls ein Abruf. Der gewählte Commit erscheint im Dashboard. Keine lokale Kopie des Quellrepositories wird erzeugt; Windows-ungültige `incoming/`-Pfade sind unkritisch.

Der Token wird ausschließlich per HTTPS-`Authorization`-Header an `api.github.com` übertragen. Kein öffentliches Raw-GitHub-URL-Verfahren. Fehlende Berechtigung führt zu einer verständlichen Fehleranzeige. Bei Netzwerkfehlern kann nur der in derselben Sitzung zuvor geladene Katalog nach Zustimmung verwendet werden. **Nach App-Neustart gibt es keinen lokalen Offline-Katalog.** Das private Upstream-Repository wird nicht verändert.

## Geführter Workflow bis zur Pod-Verwaltung

Unter **Übersicht** auf **Katalog laden und Workflows wählen** klicken. Dies lädt das private Repository über den bereits gespeicherten GitHub-Lesetoken und öffnet **Workflows**. Dort einen oder mehrere Workflows über die Checkboxen auswählen. Die Zusammenfassung zeigt die deduplizierte Modellgröße; Konflikte blockieren den nächsten Schritt.

**Auswahl übernehmen → Hardware prüfen** speichert die Auswahl und zeigt die Hardwareplanung. Die Referenzen kommen aus den README-Tabellen im privaten Repository und enthalten typische **RunPod-GPU-Typen**, VRAM-Klassen und das einzelne RunPod-Persistent-Volume. Der Client leitet daraus keine reale Thunder-Hardwareverfügbarkeit ab. Für die gesamte Instanzdisk rechnet er getrennt mit der deduplizierten Modellgröße plus 40 GB konservativer Pauschalreserve (keine gemessene Schwelle). Fehlende Größen werden nicht ersetzt.

Mit **Planungswerte übernehmen** können die Referenzwerte ausdrücklich angewendet werden: die Disk wird nur erhöht, nicht reduziert; GPU-Kandidaten werden bei vollständigen Referenzen allein nach VRAM-Klasse gefiltert. GPU-Modell-/Treiberkompatibilität ist damit **nicht** bewiesen. Prüfen und ändern: GPU-Priorität, vCPUs, GPU-Anzahl, Gesamtdisk, Template/Snapshot und Schätzpreisgrenze. Die Preise bleiben historische Schätzungen.

Nach **Pod erstellen …** erscheint die bekannte, ausdrückliche Kostenbestätigung. Bei erfolgreicher Erstellung springt der Client automatisch zur Seite **Pods verwalten**. Bestehende Pods können dort unabhängig vom Assistenten ausgewählt werden. Status aktualisieren, SSH-Terminal, privaten ComfyUI-Tunnel, Installation/Logs, Snapshots und bestätigtes Löschen verwenden. Keine unbeaufsichtigte GPU-Stopp-/Abrechnungsfunktion.

## Workflows und GPU

Unter **Workflows** mehrere Einträge auswählen. Größe und Anzahl der Modelldateien stammen aus dem Manifest; gemeinsame Dateiziele werden dedupliziert, Widersprüche verhindern den Bootstrap. Namen der Canvas-Dateien werden aus dem Tree gelesen, der Inhalt der Canvas-Dateien wird für den lokalen Katalog nicht benötigt.

Unter **Neue Instanz** GPU-Reihenfolge, vCPU-Werte, GPU-Anzahl, Disk, Template und geschätzte Kosten prüfen. Preisangaben sind *keine* verbindlichen Live-Tarife und die lokale Preisgrenze ist *keine* serverseitige Kostensperre. Eine kostenpflichtige Instanz wird erst nach ausdrücklichem Bestätigen erstellt.

## Instanzen, SSH und Bootstrap

Vor einem Create-Versuch prüft der Client vorhandene Instanzen. Nach uneindeutigem Fehler erfolgt kein automatischer zweiter Versuch. Bei erster SSH-Verbindung den ED25519-Hostfingerprint mit einer **unabhängigen** Thunder-Quelle vergleichen und nur dann bestätigen. SSH verwendet StrictHostKeyChecking=yes.

Unter **Downloads & Logs** den bestehenden Thunder-Adapter aus der vom privaten GitHub-API-Abruf gepinnten Blob-SHA starten. Der Adapter lädt remote den zentralen `scripts/pod_entry.py` und die bestehenden Downloader aus `main` des privaten Repositories. Diese **Remote-Entrypoint-Stufe ist nicht auf den angezeigten Commit fixiert**, siehe Sicherheitsreview. Ein SSH-Abbruch erfordert Prüfung des Remote-Prozesses vor einem erneuten Start.

ComfyUI nur über den privaten SSH-Loopback-Tunnel öffnen. Vor dem Löschen einer Instanz Snapshots und Outputs unabhängig prüfen.

## Logs und Updates

In **Downloads & Logs** ist das Protokoll markierbar (Strg+C) und über **Log kopieren** vollständig kopierbar. **Startdiagnose kopieren** liest die lokale `startup.log`; vor dem Teilen private Daten entfernen. Die App prüft GitHub-Releases, installiert sie aber nicht automatisch.

Der Windows-Installer ist noch nicht signiert. Einstellungen und WinCred-Tokens bleiben bei einer normalen Programmaktualisierung bestehen; das Entfernen des Programms löscht Secrets nicht automatisch. Zugangsdaten vor endgültiger Entfernung über die GUI löschen.
