# Thunder Compute Client v0.1.2 – Pod-Wiederherstellung und Downloadübersicht

## Neu

### Snapshot-basierte Pod-Wiederherstellung

Unter **Instanzen** gibt es die Aktion **„Pod wiederherstellen (Snapshot) …“** sowie **„Thunder-Podverwaltung öffnen“**. Thunder dokumentiert derzeit **keinen nativen In-Place-Neustart von GPU-Instanzen**; `sudo reboot` ist deshalb ausdrücklich **nicht** als vermeintlich sicherer Ersatz implementiert. Der unterstützte, datenerhaltende Weg ist **Snapshot → Status READY → als neue Instanz wiederherstellen**.

Die neue Aktion fragt vor dem Start ausdrücklich um Zustimmung, liest die Thunder-Instanzliste erneut und prüft die exakte Instanz-ID, UUID und den Status `RUNNING`. Sie erstellt **nur einen eindeutigen Snapshot** der persistenten Instanzdisk. Es gibt **kein implizites Pod-Create, Delete, Modify oder OS-Reboot**. Der bestehende Pod bleibt erhalten, bis der Benutzer nach abgeschlossener Wiederherstellung und Datenprüfung ausdrücklich über dessen Löschung entscheidet.

Wichtig: **Laufende Downloads/Schreibvorgänge vor dem Snapshot beenden**. Ein Snapshot ist keine Garantie für Datenintegrität laufender Schreibvorgänge und kein externer Backup-Ersatz. Der bestehende Pod und der Snapshot können weiter Kosten verursachen; eine wiederhergestellte neue Instanz benötigt eine eigene Kostenfreigabe. Im Client auf `READY` prüfen und dann die [Thunder-Podverwaltung](https://console.thundercompute.com/) zur Wiederherstellung verwenden. Die endgültige Löschung des Originals erfolgt niemals automatisch.

### Downloads & Logs

- Die Download-Tabelle hat jetzt einen **eigenen, vertikal und horizontal scrollbaren Bereich** mit Row-Virtualisierung statt unbegrenzt wachsender Höhe.
- Die Status sind anhand der tatsächlichen Upstream-Ereignisse farblich differenziert: **GELADEN/VORHANDEN = Erfolg (grün)**, **LAEUFT/FORTSCHRITT = aktiv (Akzentfarbe)**, **RETRY = Warnung**, **FEHLER = Fehlerfarbe**, **WARTET = neutral**.
- Die Farben nutzen die dynamischen Hell-/Dunkelmodus-Ressourcen des Clients.
- Der offizielle Thunder-Verwaltungslink steht unter **Instanzen** und **Downloads & Logs**. Es wird bewusst die dokumentierte offizielle Konsolenadresse ohne unbestätigte Deep-Link-Pfade geöffnet.

## Tests

.NET-Tests für Instanzidentität, Status-Revalidierung, eindeutige Snapshot-Namen und das Ausbleiben aller unerwünschten Pod-Create-/Delete-Kommandos; Windows-WPF-Smoke-Test inkl. Scrollbarkeit/Statusspalte; bestehende Remote-Supervisor- und Readiness-Tests.

## Grenzen

Die Funktion ist eine **geführte Snapshot-Wiederherstellung**, kein Neustart derselben Thunder-Instanz. Es erfolgt kein kostenpflichtiger Live-Test und kein echter Snapshot/Restore-Test auf Thunder. Modelle/Outputs sind nicht unabhängig abgesichert, sofern kein externes Backup vorliegt. Installer weiterhin nicht codesigniert; keine serverseitige Kostensperre.
