# Thunder Compute Client v0.1.0-alpha.12

**Hotfix: klare SSH-Fehlerursache, korrekte SSH-Portanzeige und sichere Systemstatus-Prüfung. Alpha, nicht produktionsfreigegeben.**

## Ursache des bisherigen GUI-Fehlers

Die Instanz wurde erfolgreich angelegt (Thunder kann den regulären numerischen `instance_id = 0` vergeben), aber `tnr connect 0 --json` schlug beim Prüfen bzw. Einrichten der SSH-Schlüssel fehl. Die offizielle CLI schreibt im JSON-Modus die **eigentliche Fehlermeldung auf stdout** in ein `error`-Feld. Der Client zeigte vorher nur stderr mit `Fetching instances... Checking SSH keys... Usage: ...` an. Diese Ausgabe reicht nicht aus, um das tatsächliche Konto-/SSH-Problem zu erkennen.

**Verbesserungen:**
- Anzeigen des von Thunder gemeldeten **SSH-Ports** neben der Pod-IP. Nicht bereitgestellte Ports werden entsprechend gekennzeichnet.
- Auswerten des strukturierten CLI-Fehlertextes statt der generischen Hilfeseite, begrenzte Ausgabe und Maskierung üblicher Tokens.
- Serielle SSH-Schlüsseleinrichtung bei mehreren GUI-Verbindungsversuchen.
- **Sicherheitskorrektur im Systemstatus:** `tnr connect --json` ist nicht rein lesend, denn bei Bedarf generiert der Thunder-Client SSH-Schlüssel. Die Systemstatus-Ansicht ruft es daher nicht mehr auf. Sie prüft den bestehenden lokalen Schlüsselpfad und führt erst bei bereits bestätigtem known_hosts einen begrenzten, harmlosen SSH-`true`-Test aus.
- Keine automatische öffentliche HTTP-Portfreigabe. ComfyUI nutzt weiterhin ausschließlich den privaten SSH-Tunnel `127.0.0.1:8188`. Öffentlich weitergeleitete HTTP-Ports können rein lesend mit `tnr ports list --json` geprüft werden.

## Sicheres Vorgehen bei weiterhin fehlender SSH-Verbindung

1. `tnr status --json --no-wait` (ID, Status und SSH-Port) und `tnr ports list --json` (optionale öffentliche HTTP-Freigaben) in PowerShell ausführen.
2. Neue Fehlermeldung unter **Downloads & Logs → Log kopieren** anschauen und private Details entfernen.
3. Nur wenn die SSH-Schlüsseleinrichtung ausdrücklich gewünscht ist: `tnr connect 0 --json` in PowerShell ausführen. Dieser Befehl kann eine neue SSH-Schlüsseldatei erzeugen und einen Schlüssel auf dem Thunder-Pod registrieren, erstellt aber keinen neuen GPU-Pod.
4. Fehlerhinweis `failed to add SSH key` bedeutet: Konto-/Pod-Schlüsselrechte bzw. Thunder-API prüfen. Ein unbekannter SSH-Fingerprint darf weiterhin nicht ungeprüft akzeptiert werden.

## Prüfung und Einschränkungen

Windows-CI: bestehende Tests sowie zusätzliche Fälle für strukturierte SSH-CLI-Fehler, numerische Instanz-ID `0`, echte Anbieterports, Tokenmaskierung und den rein lesenden Systemstatus. GUI-Smoketest aller acht Ansichten; Installer/SHA256SUMS. Kein echter SSH-End-to-End-Test mit deinem Pod, keine Kostenaktionen, keine automatische Portfreigabe. Installer nicht signiert; unabhängige Sicherheitsprüfung bleibt ausstehend. Draft-PR bleibt offen, `main` unverändert.
