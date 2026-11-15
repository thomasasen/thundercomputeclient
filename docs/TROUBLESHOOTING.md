# Fehlerdiagnose

## Die GUI startet nicht

Aktuelle Alpha-Version installieren (Windows 11 x64). In PowerShell:

```powershell
& "$env:LOCALAPPDATA\Programs\ThunderClient\ThunderClient.exe"
notepad "$env:LOCALAPPDATA\ThunderClient\logs\startup.log"
```

Die Datei kann fehlen, wenn noch kein Diagnoseereignis aufgetreten ist. Vor dem Teilen auf enthaltene Pfade, Namen und Zugangsdaten prüfen. Tokens niemals veröffentlichen.

## Beim Start: Access to the path 'pack-...idx' is denied

Das ist ein Datei-Zugriffsproblem im lokalen Git-Checkout oder beim Aufräumen eines gescheiterten Klons. **Die genaue Ursache auf dem jeweiligen Rechner lässt sich aus dieser Meldung allein nicht bestimmen.** Mögliche Ursachen sind ein gescheiterter privater Git-Clone, geöffnete Dateien, Dateiberechtigungen oder Sicherheitssoftware.

In frühen Versionen wurde ein ursprünglich fehlgeschlagener Clone durch die nachfolgende Fehlermeldung beim Löschen einer gesperrten bzw. schreibgeschützten `.idx`-Datei überdeckt. Der Git-Fehler wird jetzt erhalten; ein App-übergreifender Sync-Lock verhindert gleichzeitige Änderungen am selben Checkout. Bestehende Verzeichnisse werden nie blind gelöscht.

Prüfen in PowerShell (fragt bei Bedarf den Git Credential Manager):

```powershell
git --version
git ls-remote https://github.com/thomasasen/runpod-ai-workflows.git HEAD
```

Wenn `git ls-remote` scheitert, zuerst die Git-Anmeldung und **Read-only-Zugriffsrechte auf das private Repository** herstellen. Der GitHub-PAT im Tab „Zugangsdaten“ wird nur für den Remote-Bootstrap gespeichert und **ist kein Ersatz** für die Git-Credential-Manager-Anmeldung.

Wenn `git ls-remote` funktioniert, aber Klonen/Fetch weiterhin an einer `.idx`-Datei scheitert, die vollständige, bereinigte Meldung aus dem App-Status und dem Startprotokoll prüfen. Den Workflow-Checkout nicht unüberlegt löschen; er kann eigene Änderungen enthalten. Keine Firewall-, Defender- oder Sicherheitssoftware pauschal deaktivieren.

## Thunder-Instanzen fehlen

Die CLI in einer regulären PowerShell unabhängig prüfen:

```powershell
tnr status --json --no-wait
```

Erst `tnr login` durchführen, falls die CLI selbst nicht angemeldet ist. Die Anwendung kann trotz Git-Synchronisierungsfehler Statusabfragen versuchen; Git und Thunder haben getrennte Anmeldungen.

## Fehlerbericht

Benötigt werden: exakte App-Version, ob das Fenster sichtbar ist, letzte bereinigte Fehlermeldung, Ergebnis von `git ls-remote` ohne sensible Repo-Inhalte, sowie relevante Zeilen aus `startup.log`. Bitte keine Tokens oder SSH-Schlüssel senden.

## Protokoll aus der Anwendung kopieren

Im Bereich **Downloads & Logs** können Protokollzeilen direkt mit der Maus markiert und per `Strg+C` kopiert werden. **Log kopieren** übernimmt das gesamte sichtbare Sitzungsprotokoll. **Startdiagnose kopieren** liest die lokale `startup.log` und kopiert die bereinigte Ausgabe. Die App zeigt den Kopierstatus unten im Fenster an. Vor dem Teilen auf private Pfade, Namen und Zugangsdaten prüfen.
