# Fehlerdiagnose

## Git-Fehler `invalid path incoming/Krea 2 + FLUX.2: ...`

Dieser Fehler der Vorgängerversionen stammt von einem Windows-ungültigen Verzeichnisnamen mit Doppelpunkt im privaten Quellrepository. Selbst der nachträglich eingeführte Sparse Checkout der `alpha.5`/`alpha.6` konnte den Fehler auf dem betroffenen Benutzer-PC nicht verhindern.

**Ab alpha.7 liest die GUI ausschließlich über die authentifizierte GitHub-API. Es wird kein Git-Repository geklont oder ausgecheckt.** Die `incoming/`-Pfade werden nie in das Windows-Dateisystem übernommen. Bereits vorhandene temporäre `.clone-...`-Ordner aus älteren Installationen werden **nicht automatisch gelöscht**.

Wenn der Katalog nicht lädt: unter **Zugangsdaten** einen Fine-grained GitHub-PAT mit **Contents: Read-only** für `thomasasen/runpod-ai-workflows` hinterlegen. Danach **Katalog aktualisieren**. Die neue Fehlermeldung enthält bei GitHub-Zugriffsproblemen den HTTP-Status (401/403/404) in verständlicher Form. Weder Git for Windows noch der Git Credential Manager sind für den Katalog erforderlich.

## Thunder-CLI

Unter Windows in PowerShell prüfen:

```powershell
Get-Command tnr
tnr status --json --no-wait
tnr snapshot list --json --no-wait
```

Auf dem betroffenen Gerät wurde `tnr.exe` bereits unter `C:\Program Files (x86)\tnr\tnr.exe` gefunden. Seit alpha.6 kann die GUI zusätzlich zu PATH diesen Standardinstallationsort verwenden und liefert bei einem Startfehler die native Windows-Fehlernummer. Der bloße Fund von `tnr.exe` beweist noch nicht, dass der Prozess ausgeführt werden kann.

## SSH-Fehler: „SSH-Hostschlüssel konnte nicht abgefragt werden“

Der Client ruft vor der erstmaligen SSH-Verbindung `ssh-keyscan -T 8 -p <PORT> -t ed25519 <IP>` auf. Die bisherige Alpha zeigte bei leerer Ausgabe nur eine generische Meldung. Das beweist **weder** einen falschen SSH-Schlüssel **noch** eine fehlerhafte Benutzeranmeldung: Der SSH-Dienst kann noch starten, der angegebene Port kann blockiert sein oder der Dienst bietet keinen ED25519-Schlüssel an.

Ab `v0.1.0-alpha.10` gibt es bis zu drei begrenzte Versuche und die letzte `ssh-keyscan`-Diagnose erscheint in der Fehlermeldung. Es wird vorzugsweise `C:\\Windows\\System32\\OpenSSH\\ssh-keyscan.exe` statt einer möglicherweise abweichenden Git-Installation verwendet.

Prüfung auf dem Benutzerrechner, ohne eine weitere kostenpflichtige Instanz zu erstellen:

```powershell
Get-Command ssh-keyscan
tnr status --json --no-wait
```

Aus `tnr connect <INSTANZ-ID> --json` die für die Verbindung vorgesehenen Werte `ip` und `port` entnehmen. **Keine komplette Ausgabe oder private Schlüsseldateien in Chats posten.** Danach mit den tatsächlichen Werten, nicht mit den Platzhaltern, prüfen:

```powershell
Test-NetConnection -ComputerName <IP> -Port <PORT>
ssh-keyscan -T 8 -p <PORT> -t ed25519 <IP>
```

`TcpTestSucceeded: False` deutet auf Netzwerkerreichbarkeit/Port/SSH-Dienst hin. Wird per `ssh-keyscan` **kein** `ssh-ed25519` geliefert, kann die App keinen Fingerprint zur unabhängigen Bestätigung anzeigen. Bei fehlender Erreichbarkeit den Startstatus des Pods kontrollieren und nach kurzer Zeit erneut verbinden. Gegebenenfalls mit der offiziellen Thunder-Konsole den Manual-SSH-Endpunkt abgleichen.

**Sicherheit:** Niemals `StrictHostKeyChecking` deaktivieren, nie `known_hosts` pauschal löschen oder einen unbekannten Fingerprint ungeprüft akzeptieren. `ssh-keyscan` stellt **keine** Identitätsbestätigung dar. Der Fingerprint muss unabhängig verifiziert werden.

## Log kopieren

In **Downloads & Logs** markieren und Strg+C drücken, für das gesamte Log **Log kopieren**. **Startdiagnose kopieren** kopiert die lokale Diagnose nach Redaktionsregeln. Vor dem Teilen auf Token, private Pfade und andere persönliche Informationen prüfen.

## Startdiagnose

```powershell
& "$env:LOCALAPPDATA\Programs\ThunderClient\ThunderClient.exe"
notepad "$env:LOCALAPPDATA\ThunderClient\logs\startup.log"
```

Das Startprotokoll kann fehlen, wenn noch kein Eintrag erzeugt wurde. Private Token oder SSH-Schlüssel niemals in Fehlerberichte kopieren.
