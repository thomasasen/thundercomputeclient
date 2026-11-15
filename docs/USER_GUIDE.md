# Benutzeranleitung

## Ersteinrichtung

1. Offizielle Thunder-CLI von https://github.com/Thunder-Compute/thunder-cli/releases/latest installieren und mit tnr login anmelden.
2. Git for Windows und Git Credential Manager installieren. Auf das private Repository https://github.com/thomasasen/runpod-ai-workflows mit einem berechtigten Konto Zugriff sicherstellen. Der Client fordert kein Git-Passwort an und gibt kein PAT als Git-Prozessargument weiter.
3. OpenSSH Client, ssh-keygen und ssh-keyscan in PATH verfügbar machen.
4. In Zugangsdaten ein möglichst eng begrenztes GitHub fine-grained PAT (Contents: Read-only für runpod-ai-workflows) für den **Remote-Bootstrap** im Credential Manager speichern; gegebenenfalls Hugging Face und Civitai ergänzen.
5. Client starten. Beim ersten Mal wird das Repository unter LocalAppData\ThunderClient\runpod-ai-workflows geklont, bei späteren Starts geprüft und nur Fast-Forward aktualisiert.
6. Wenn GitHub oder die Anmeldung nicht funktioniert, den lokalen Offline-Stand nur bei ausdrücklicher Nachfrage freigeben. Bei lokalen Änderungen wird kein Pull erzwungen.

## Workflow und GPU

Unter Workflows mehrere Workflows markieren. Modellanzahl und Modellgrößen werden ohne feste Werte direkt aus model.json gelesen. Varianten werden pro Ordner angezeigt. Bei unbekannten Modellgrößen und doppelten Dateizielen vor der Installation prüfen.

Unter Neue Instanz die GPU-Reihenfolge mit Drag-and-drop ändern. Die drei vorbelegten GPUs (A6000, L40, A100) sind Startbeispiele, keine Live-Verfügbarkeitsliste. vCPU-Werte sind editierbar; die tnr-CLI ist die autoritative Validierungsquelle. Die geschätzten GPU-Preise müssen vor dem kostenpflichtigen Start gegen aktuelle Thunder-Tarife geprüft werden. Ein lokaler Stundenpreis-Maximalwert ist **keine** serverseitig erzwungene Kostensperre.

Für T2I_MOSS-Image-Fast sind im aktuellen externen Manifest ungefähr 108,78 GB und 80 Modelldateien dokumentiert (Stand 15.11.2026). 135 GB Gesamtdisk ist eine knappe Testkonfiguration, keine zugesicherte Mindestgröße. System, Python-venvs, Caches und Outputs brauchen zusätzlichen Platz. Standardwert im Client ist vorsichtiger 160 GB, ebenfalls nicht live validiert.

## Instanz und SSH

Vor einer Erstellung ist ein ausdrücklicher Dialog zu bestätigen. Der Client sperrt automatische Erstellung, wenn bereits eine Instanz gemeldet wird. Bei eindeutiger GPU-Kapazitätsabsage kann er eine niedrigere Priorität probieren; bei anderen Fehlern bricht er ab. Nach einem Verbindungsabbruch nicht ungeprüft erneut klicken.

Bei erster Verbindung zeigt der Client den ED25519-Hostfingerprint. Erst wenn dieser **unabhängig** in Thunder geprüft ist, bestätigen. Der Client nutzt danach StrictHostKeyChecking=yes. Ein Wechsel des Hostschlüssels darf nicht blind akzeptiert werden.

Unter Downloads & Logs den bestehenden thunder/bootstrap.sh vom lokalen Checkout ausführen. Das übertragene Skript ruft über das private Repo scripts/pod_entry.py und dessen vorhandenen Downloader und Custom-Node-Installer auf. Tokens liegen nicht in Prozessargumenten, können aber während des Bootstrap-Prozesses im Remote-Prozessspeicher vorkommen. Nach einer SSH-Unterbrechung den Remote-Zustand vor einem manuellen Neustart prüfen. Automatische Reconnect-/Resume-Steuerung ist noch nicht implementiert.

ComfyUI nur mit dem Button über localhost öffnen, niemals öffentlich die Thunder-HTTP-Ports freigeben. Für Debugging kann ein SSH-Terminal geöffnet werden.

## Snapshot und Löschen

Nur RUNNING-Instanzen können via tnr snapshot create gesichert werden. Erst bei Status READY für Wiederherstellung verwenden. Das Auswählen eines READY-Snapshots setzt ihn als Template für einen **später separat bestätigten** kostenpflichtigen Create-Versuch. Vor dem Löschen prüfen, ob der Snapshot wirklich fertig ist und Outputs separat gesichert sind; Thunder berechnet Snapshot-Speicher möglicherweise zusätzlich.

## Updates / Deinstallation

Unter Einstellungen Release-Version prüfen. Der Client lädt und installiert keine Fremdsoftware selbst. Bei Installationspaketen aus GitHub Actions/Releases auf Quelle und SHA-256-Hash achten. Das Installer-Uninstall entfernt Programmdateien, nicht den privaten Workflow-Checkout oder Geheimnisse im Windows Credential Manager. Zugangsdaten im Programm vor vollständigem Entfernen ausdrücklich löschen.

## Diagnose

Logs werden bei Anzeige und Export redigiert, soweit bekannte Tokens und Standardtoken-Muster erkannt werden. Vor einer Weitergabe von Diagnose-Dateien dennoch manuell auf sensible Daten, interne IPs und Workflow-Details prüfen. GitHub- und Thunder-Netzwerkfehler erscheinen im Dashboard; zusätzliche Provider-Details über die offizielle Thunder-Konsole verifizieren.

