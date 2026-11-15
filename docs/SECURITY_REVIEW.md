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

## Ein-Klick-Bereitstellung: Kosten und SSH-Vertrauen

- Planung nur aus ausgewählten, authentifizierten Workflow-Manifesten, bei unbekannten Modelldateigrößen, Kollisionen oder expliziten Manifest-Autorisierungsflags kein automatischer Start. GPU-/VRAM-Referenzen aus RunPod-READMEs begründen keine sichere Thunder-Laufzeitkompatibilität.
- Für einen **neuen** Pod ist die ausdrückliche Kostenfreigabe mit GPU-Reihenfolge, Disk und historischer 1/4/12-Stunden-Schätzung zwingend. `CreateWithFallbackAsync` prüft vor und nach Create-Versuchen, erlaubt nur belegte Kapazitätsfehler als Fallback und verhindert Doppelstarts via Lock. Vorhandene einzelne Pods werden nur nach erneutem Bestätigen eingerichtet; bei mehreren Pods wird abgebrochen.
- Der SSH-Hostschlüssel wird nie automatisch angenommen; ein neuer Fingerprint muss unabhängig verifiziert werden. Der Client nutzt `tnr connect --json` ausdrücklich erst nach Nutzerfreigabe, weil der Befehl ggf. einen neuen Schlüssel anlegt.
- Bei unklarem Verbindungs-/Downloadfehler keine wiederholten Provisionierungen, keine automatisch ausgelösten Löschungen oder Snapshots. Der Button **Automatik abbrechen** unterbricht die Verbindung, nicht die kostenpflichtige Instanz.
- Vor der kostenpflichtigen Vollinstallation wird bei Civitai-Modellen ein Civitai-Key verlangt. Das ersetzt weder Lizenz-/Gated-Freigaben noch das Upstream-Autorisierungs-Preflight.
- Der bestehende SSH-stdin-Bootstrap überträgt die Tokens im Prozesskontext, ohne sie in CLI-Argumenten, Git-URLs oder Logmeldungen abzulegen. Andere Prozesse desselben Linux-Benutzers können unter Umständen Umgebungsvariablen lesen.
- Upstream-Prozess geht per `exec` in ComfyUI über. **Kein langlebiger Supervisor, kein sicheres Auto-Resume nach GUI-Abbruch**. Der Remote-Entrypoint wird weiter dynamisch von `main` geladen: zwischen lokaler Katalogprüfung und Remote-Start kann der GitHub-Stand wechseln. Beides bleibt vor Stable-Release zu beheben.

## SSH-CLI-Schlüsselprovisionierung und Porttrennung

Thunder `tnr connect <id> --json` liest nicht nur Daten: Wenn kein lokaler Key existiert, kann die CLI einen SSH-Schlüssel serverseitig registrieren und lokal speichern. Daher verwendet die neue Systemstatus-Prüfung **keinen** `tnr connect`-Aufruf mehr; sie prüft den erwarteten lokalen Schlüsselpfad per `File.Exists` und versucht nur bei bereits bestätigtem known_hosts einen zeitbegrenzten, nicht schreibenden SSH-`true`-Befehl.

Ein explizites GUI-Verbindungsereignis ruft `tnr connect <id> --json` weiter auf; parallele Aufrufe des gleichen ThunderCli-Objekts werden per Semaphore serialisiert, um konkurrierende SSH-Key-Provisionierungen zu vermeiden. CLI-Fehlermeldungen aus dem JSON-`error`-Feld auf stdout werden begrenzt und redigiert; keine vollständigen JSON-Antworten oder CLI-Usage-Seiten werden ungeprüft als Fehlermeldung übernommen.

SSH-Port und ComfyUI-Port sind getrennt: der von Thunder gemeldete SSH-Port verbindet zur Instanz, während `127.0.0.1:8188` ausschließlich als lokaler SSH-Forward genutzt wird. **Keine öffentliche HTTP-Portfreigabe** wird automatisch konfiguriert. Hostfingerprint-Vertrauen bleibt zwingend.

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

