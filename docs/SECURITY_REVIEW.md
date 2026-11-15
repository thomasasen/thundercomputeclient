# Sicherheitsreview und Red-Team-Checkliste

## Bedrohungsmodell

Schutzgüter: GitHub-PAT, HF/Civitai-API-Tokens, SSH-Schlüssel, lokal eingelesene private Workflows und Bootstrap-Code, Remote-ComfyUI, GPU-Kostenguthaben, Modelle/Outputs auf der Instanzdisk. Angreifer: manipulierter DNS/SSH-Host, unberechtigter lokaler Prozess desselben Benutzers, manipuliertes Workflow-Repository, bösartige CLI-Ausgabe, unklare Thunder-Create-Antwort, ungeprüftes Update oder ungesicherte öffentliche Ports.

## Implementierte Kontrollen (statisch, nicht live bewiesen)

1. GitHub-Upstream ausschließlich lesen: an festem HTTPS-`api.github.com`-Host ohne Redirect; Fine-grained-PAT aus WinCred mit Contents: Read-only; Commit/Tree/Blob-SHA-Konsistenz; kein Git-Checkout, keine privaten Dateien als Disk-Cache.
2. Kein Token in URLs, Git/JSON, Prozessargumenten oder gespeichertem Cache. API-Authorization ausschließlich per HTTPS-Header. Eigener WinCred-Namespace, maskierte Anzeige, Löschfunktion.
3. Remote-Bootstrap über OpenSSH-Standard-Eingabe, kein persistenter Secret-File-Schritt. Lokaler Adapter wird über Commit-gepinnten GitHub-Blob gelesen; die separate Remote-Pod-Entry-Stufe nutzt weiter main.
4. SSH StrictHostKeyChecking=yes; ED25519-Fingerprint vor erstmaliger Hinterlegung explizit und anhand unabhängiger vertrauenswürdiger Thunder-Quelle bestätigen. Host/Port und ED25519-Keyblob werden validiert. Bounded Retry nur für Keyscan ohne Treffer; kein Auto-Trust und keine Abschwächung auf unbekannte Hostschlüssel.
5. Private ComfyUI-Anbindung über SSH 127.0.0.1:8188, keine Portfreigabe über Thunder.
6. Einmaliger kostenpflichtiger Erstellungsdialog, lokale exklusive Create-Sperre, status-before/status-after, automatischer Fallback nur bei positivem Kapazitätsmuster.
7. Historische Preis-Schätzungen ausdrücklich gekennzeichnet; keine falsche harte Kostenbegrenzung.
8. Snapshot/Deletion mit ausdrücklichen Bestätigungen, Backup-Hinweisen; keine automatische Wiederholung eines abgebrochenen Bootstrap.
9. Tests mocken alle externen Aufrufe, sodass keine Kosten und Downloads ausgelöst werden.

## Systemstatus-Sicherheitsgrenzen

- Die Systemprüfung wird **nur explizit auf Klick** ausgeführt; sie ruft keinerlei `tnr create`, `tnr delete`, Bootstrap oder Modelldownload auf.
- Prüft gespeicherte Tokens ausschließlich über lesende HTTPS-Endpunkte mit Bearer-Authorization-Headern, ohne Weiterleitung/Cookies und ohne Kopieren von Antworttexten oder Secrets in Logs. Drittanbieter-Schlüssel werden beim Prüfen an ihren jeweiligen Anbieter übertragen.
- Ein gespeicherter Token oder eine gefundene SSH-Key-Datei wird als **vorhanden** unterschieden von **erfolgreich authentifiziert**. Das ist kein Sicherheitsattest aller Token-Berechtigungen oder Modelle.
- SSH-Hostfingerprint wird nicht allein aufgrund einer `ssh-keyscan`-Antwort als vertrauenswürdig eingestuft; Hostvertrauen muss im gesonderten Dialog ausdrücklich und unabhängig bestätigt worden sein. Status-Check testet SSH erst bei bereits bekanntem Host.
- Zeitstempel beziehen sich auf Prüfungen innerhalb der aktuellen App-Sitzung, nicht auf fortlaufende Überwachung. Bei Tokenänderungen werden entsprechende Prüfzustände ungültig gemacht.
- Fest kodierte offizielle Dokumentations-/Accountlinks mit HTTPS-Host-Whitelist. Keine von privaten Manifesten stammenden externen Navigations-URLs.

## Ungeklärte oder offene Risiken

- Keine unabhängige Prüfung durch zweite Person/Instanz dokumentiert. Dieses Dokument ist eine vorbereitete Checkliste und stellt **keine** unabhängige Freigabe dar.
- Die lokale Windows-Benutzeridentität besitzt Zugriff auf eigene Credential-Manager-Secrets; Malware unter derselben Identität kann diese lesen.
- Ein privates GitHub-Repository ist Quelle für ausführbaren Remote-Bootstrap-Code. Repository-Zugriffsrechte und Branchschutz sind sicherheitskritisch. Der Client lädt den Adapter per Commit-gepinntem Blob, aber der vorhandene Remote-Adapter lädt seinen pod_entry von main; das kann zwischen Synchronisierung und Start wechseln. Ein SHA-gepinnter, verifizierter Deployment-Pfad ist noch nicht implementiert.
- SSH-Trust-On-First-Use-Fingerprint muss wirklich unabhängig geprüft werden. Ungeprüfte Benutzerbestätigung wäre eine MITM-Gefahr.
- Remote-Prozessumgebung kann Secrets für denselben Linux-Benutzer sichtbar machen. Kein Remote Secret Store implementiert.
- Kein Fortsetzungsprotokoll für SSH-Abbruch; nach Verbindungsverlust muss die tatsächliche Remote-Tätigkeit überprüft werden.
- Kostenangebot ist nicht live. Ein zulässiger Schätzwert kann vom später abgerechneten Preis abweichen.
- Exportredaktion nutzt bekannte Tokens und Muster; unbekannte Secretformate können zusätzliche manuelle Redaktion erfordern.
- Keine authentifizierte Code-Signatur für den Windows-Installer; Hashdatei allein schützt nicht vor kompromittiertem Repository/Build. Updates werden deshalb nicht automatisch installiert.
- Kein echter Thunder-, Snapshot-, Downloader-, SSH- oder Windows-Installer-Livetest durchgeführt.

## Vor Merge verpflichtend

- Auf Windows erfolgreiche CI, kontrollierte Installerinstallation/Deinstallation, Live-Test des authentifizierten privaten GitHub-API-Zugriffs mit begrenztem PAT.
- Zweite, unabhängig durchgeführte Sicherheitsprüfung aller Secret-, SSH-, Create- und Updatepfade, inklusive Negativtests.
- Statische Analyse ohne High-Severity-Funde, Tests der Race-/Fehlerfälle.
- Keine GPU-Livetests im Rahmen dieses Auftrags. Runtime-Risiken müssen verbleibend benannt werden.

