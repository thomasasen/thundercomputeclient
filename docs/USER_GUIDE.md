# Benutzerhandbuch

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
