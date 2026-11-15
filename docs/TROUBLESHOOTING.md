# Windows-SSH-Hostschlüssel: `choose_kex: unsupported KEX method sntrup761x25519-sha512@openssh.com`

**Betroffen:** Windows-OpenSSH-`ssh-keyscan.exe` (bekannter Upstream-Fehler [PowerShell/Win32-OpenSSH #2140](https://github.com/PowerShell/Win32-OpenSSH/issues/2140)); er tritt bei modernen Linux-OpenSSH-Servern auf. **Dies ist kein Beweis für einen falschen SSH-Port oder fehlenden ED25519-Hostkey.** In einem reproduzierten Nutzerlauf war der Pod bereits `RUNNING`, IP und Port vorhanden, jedoch brach die Hostschlüsselabfrage ab, bevor ein Bootstrap/Modelldownload begann.

**Ab `v0.1.0-alpha.14`:** Nach dem konkreten `choose_kex`-Fehler greift die GUI ohne Neu-Erstellung der VM auf einen isolierten `ssh.exe`-Handshake zurück. Kein SSH-Privatkey, keine Anmeldung und kein Remote-Kommando in der Abfrage. `ssh.exe` speichert den **unbestätigten öffentlichen** ED25519-Key ausschließlich in einer zufällig benannten Datei unter TEMP. Der Client prüft die Zeile und den SHA-256-Fingerprint für exakt die erwartete IP und den SSH-Port, löscht die Datei und zeigt weiterhin den **verpflichtenden Fingerprint-Vergleich** an. Erst nach ausdrücklicher Benutzerbestätigung wird der Key in `ThunderClient/known_hosts` übernommen. Spätere Sessions bleiben bei `StrictHostKeyChecking=yes`. Wenn auch der Fallback keine Hostkey-Zeile liefert, stoppt die Bereitstellung mit einer konkreten Fehlermeldung und **ohne** automatischen zweiten Pod.

**Für einen schon erstellten Pod:** Den Client aktualisieren; unter **Instanzen** prüfen, ob der Pod noch läuft und Kosten verursacht. Bei genau einer Instanz **„Bestehenden Pod automatisch einrichten“** benutzen. Es wird nach bestätigter Wiederverwendung kein zweiter Pod angelegt. Falls du den Pod nicht benötigst, nach Prüfung von Outputs/Snapshot unter **Instanzen** ausdrücklich löschen. Der Client löscht GPU-Kosten nicht stillschweigend.

**Nur zur optionalen, read-only Diagnose in PowerShell:**

```powershell
Get-Command ssh, ssh-keyscan
ssh -V
tnr status --json --no-wait
```

`ssh-keyscan` kann den beschriebenen Bug weiterhin anzeigen; die neue GUI umgeht ihn. Bitte keine `StrictHostKeyChecking=no`-Option setzen und keinen unbekannten Fingerprint ungeprüft bestätigen. Die offizielle Fehlerquelle ist [PowerShell/Win32-OpenSSH #2140](https://github.com/PowerShell/Win32-OpenSSH/issues/2140); ein [Git-for-Windows-`ssh-keyscan`](https://github.com/actions/runner-images/issues/13072) ist ein dokumentierter manueller Ersatz, **nicht** für die GUI erforderlich.

---

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

## SSH-Fehler: `tnr connect --json` scheitert bei „Checking SSH keys…“

Die Instanz kann bereits `RUNNING` sein (Thunder-Instanz-ID `0` ist gültig), während `tnr connect 0 --json` noch scheitert. Der offizielle CLI-Ablauf prüft vor einer SSH-Anmeldung Schlüssel und kann bei einem fehlenden lokalen Schlüssel über die Thunder-API einen neuen Schlüssel generieren und lokal speichern.

In frühen Desktop-Versionen wurde nur `stderr` ausgegeben: Fortschritt und `Usage:` verdeckten den eigentlichen CLI-Fehler, der bei `--json` als `{"error":"..."}` auf `stdout` steht. **Ab `alpha.12` liest der Client den strukturierten Fehler** und zeigt die diagnostische Ursache an; derselbe CLI-Prozess wird nicht von zwei GUI-Aktionen gleichzeitig zur Schlüsselanlage verwendet.

**Prüfen, ohne etwas zu verändern:**

```powershell
tnr status --json --no-wait
tnr ports list --json
```

Damit werden Instanz-ID, UUID, IP und der von Thunder bereitgestellte SSH-Port sowie eventuell aktivierte öffentliche HTTP-Ports ermittelt. **Der SSH-Port ist nicht der ComfyUI-Port 8188.** SSH wird in der Pod-Ansicht mit dem tatsächlichen Port angezeigt. Der Client nutzt für ComfyUI einen **privaten SSH-Tunnel** auf dem lokalen Loopback-Port 8188; es ist keine öffentliche Freigabe von 8188 nötig.

**Nur wenn SSH bewusst eingerichtet werden soll:**

```powershell
tnr connect 0 --json
```

Dieser Befehl kann bei fehlendem SSH-Schlüssel eine Schlüsselanlage in Thunder auslösen. Er erstellt **keinen neuen GPU-Pod**, ist aber nicht streng lesend. Bei einem Fehler nur die `error`-Eigenschaft aus der JSON-Ausgabe und die relevanten Fortschrittszeilen teilen; **keinen privaten Schlüssel**, Token oder vollständige persönliche Pfade senden. Falls die CLI mit `failed to add SSH key` antwortet, die Thunder-Kontoberechtigung und die SSH-Key-Funktion des Pods prüfen.

Die Systemstatus-Ansicht prüft seit `alpha.12` den lokalen Schlüssel nur über den erwarteten Dateipfad; fehlende lokale Schlüssel werden als Einrichtung nötig und **nicht** als erfolgreiche SSH-Verbindung gewertet.

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
