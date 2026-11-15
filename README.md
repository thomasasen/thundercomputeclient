# Thunder Compute Client

Nativer Windows-Desktop-Client (.NET 10/WPF) für ComfyUI-Workflows aus dem **privaten**, unveränderten GitHub-Repository `thomasasen/runpod-ai-workflows`.

**Status: Alpha-Vorabversion v0.1.0-alpha.16, keine Produktionsfreigabe.** Die Windows-CI prüft Kompilierung, Mock-/Regressionstests, den tatsächlichen WPF-Fensterstart mit allen acht Bereichen und den Inno-Setup-Installer. Kein echter Thunder-/GPU-/SSH-/Modelldownload-End-to-End-Test.

## Ein-Klick-Bereitstellung

Nach dem automatischen Laden des privaten Katalogs auf **Workflows** einen oder mehrere Workflows auswählen und **„Automatisch bereitstellen: Pod + ComfyUI + Modelle …“** anklicken. Alternativ unter **Neue Instanz** die Hardware vorher anpassen und **„Pod + Modelle automatisch starten …“** wählen.

1. Der Client berechnet die deduplizierte Modellgröße aus den authentifizierten `model.json`-Manifesten sowie eine Gesamtdisk mit der dokumentierten pauschalen Reserve. README-VRAM-Angaben sind **RunPod-Referenzen, keine Thunder-Messungen**. Er berücksichtigt nur bereits aktivierte GPUs und erhöht **niemals automatisch die Preisgrenze**. Fehlende Größen, widersprüchliche Modelldateien und gesondert zu bestätigende Manifestautorisierungen blockieren den automatischen Start.
2. **Vor der kostenpflichtigen Erstellung** erscheinen die konkrete GPU-Reihenfolge, vCPUs, Disk, geschätzte Kosten für 1/4/12 Stunden und die aktuellen Grenzen. Erst nach dem bestätigenden Klick auf **Ja** wird ein Pod angelegt. **Existiert bereits genau ein Pod, wird dieser nach eigener Zustimmung wiederverwendet.** Bei mehreren Pods wird aus Sicherheitsgründen abgebrochen, statt versehentlich den falschen Pod zu wählen.
3. Der Client wartet auf den vom Anbieter gemeldeten Status `RUNNING` mit gültiger SSH-IP und Port, bereitet den SSH-Schlüssel über die offizielle `tnr`-CLI vor und verlangt bei einem neuen Host die **unabhängige ED25519-Fingerprint-Bestätigung**. Ist SSH nicht bereit, wird **kein neuer GPU-Pod** erzeugt.
4. Anschließend startet der Client den vorhandenen `thunder/bootstrap.sh` aus der privaten GitHub-API im per SSH geöffneten Pod. Der Upstream-Bootstrap installiert ComfyUI, seine Nodes und die ausgewählten Workflows. Er startet die **bestehenden parallelen Modell-/Node-Downloads** mit adaptivem, auf **maximal vier gleichzeitige Datei-Jobs** begrenztem Download-Scheduler. `Downloads & Logs` zeigt die Echtzeitprotokolle, Modellfortschritte und die vom Upstream gemeldete ComfyUI-HTTP-Bereitschaft. Ein bereits antwortender ComfyUI-Dienst wird nicht ungefragt durch einen zweiten Bootstrap überlagert.
5. Unter **Instanzen** kann ComfyUI wie bisher über den privaten SSH-Tunnel geöffnet werden. Für diesen Modus wird **kein öffentlicher ComfyUI-Port** eingerichtet.

**Betriebshinweise:** Der erste Ein-Klick-Modus hält die SSH-/Bootstrap-Verbindung zur laufenden ComfyUI-Sitzung offen. **Die Desktop-App muss dabei geöffnet bleiben.** Bei App-Abbruch, Netzwerkunterbrechung oder unklarem SSH-/Download-Fehler erfolgt **kein blinder automatischer Neustart**. Der bestehende Pod kann weiterhin Kosten verursachen; der Benutzer muss den Remote-Zustand vor erneuter Einrichtung prüfen. **„Automatik abbrechen“ löscht keine Instanz**. Die VM wird auch bei fehlgeschlagenen Downloads nicht automatisch gelöscht.

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

## Bedienung

1. App starten und unter **Zugangsdaten** den privaten GitHub-Lesetoken speichern, dann **Katalog aktualisieren**. Ohne Token bleibt die GUI offen und zeigt eine aussagekräftige Fehlermeldung.
2. Unter **Workflows** Einträge wählen. Einmalig geladene Metadaten werden in der laufenden Sitzung gehalten; eine neue Sitzung lädt die Daten erneut.
3. Unter **Neue Instanz** GPU-Reihenfolge, vCPUs, Disk, Template und geschätzte Stundengrenze prüfen. Die erste kostenpflichtige Erstellung braucht immer eine ausdrückliche Bestätigung.
4. Unter **Instanzen** Status/Snapshots abrufen, SSH-Fingerprint unabhängig verifizieren und dann über **Downloads & Logs** den Bootstrap starten.
5. ComfyUI über den **privaten SSH-Loopback-Tunnel** öffnen; vor dem Löschen fertiggestellte Sicherungen prüfen.

In **Downloads & Logs** ist das Protokoll schreibgeschützt, markierbar und mit Strg+C kopierbar. „Log kopieren“ und „Startdiagnose kopieren“ verwenden die Windows-Zwischenablage; private Informationen vor dem Teilen prüfen.

## Lokaler Build

```powershell
dotnet restore tests/ThunderClient.Tests/ThunderClient.Tests.csproj
dotnet test tests/ThunderClient.Tests/ThunderClient.Tests.csproj -c Release
dotnet publish src/ThunderClient/ThunderClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish
```

Der Windows-Installer wird in GitHub Actions erstellt; er ist noch nicht codesigniert. Die App installiert Releases nicht automatisch. **Preise sind unverifizierte Schätzungen und keine serverseitigen Kostenlimits.** Der Benutzer ist selbst für die Kontrolle kostenpflichtiger Ressourcen verantwortlich.

Siehe [Benutzerhandbuch](docs/USER_GUIDE.md), [Architektur](docs/ARCHITECTURE.md), [Sicherheitsgrenzen](docs/SECURITY_REVIEW.md), [Fehlerdiagnose](docs/TROUBLESHOOTING.md) und [Implementierungsstand](docs/IMPLEMENTATION_STATUS.md).
