# Sicherheitsreview und Red-Team-Checkliste

## Bedrohungsmodell

Schutzgüter: GitHub-PAT, HF/Civitai-API-Tokens, SSH-Schlüssel, lokaler Workflow-Checkout, Remote-ComfyUI, GPU-Kostenguthaben, Modelle/Outputs auf der Instanzdisk. Angreifer: manipulierter DNS/SSH-Host, unberechtigter lokaler Prozess desselben Benutzers, manipuliertes Workflow-Repository, bösartige CLI-Ausgabe, unklare Thunder-Create-Antwort, ungeprüftes Update oder ungesicherte öffentliche Ports.

## Implementierte Kontrollen (statisch, nicht live bewiesen)

1. GitHub-Upstream ausschließlich lesen und synchronisieren; Origin-Prüfung; nur sauberer main-Checkout; Fast-Forward-only.
2. Kein Token in Git/JSON, Git-URLs oder Prozessargumenten. Eigener WinCred-Namespace, maskierte Anzeige, Löschfunktion.
3. Remote-Bootstrap über OpenSSH-Standard-Eingabe, kein persistenter Secret-File-Schritt. Bootstrapskript stammt aus lokal synchronisiertem Checkout.
4. SSH StrictHostKeyChecking=yes; ED25519-Fingerprint vor erstmaliger Hinterlegung explizit bestätigen.
5. Private ComfyUI-Anbindung über SSH 127.0.0.1:8188, keine Portfreigabe über Thunder.
6. Einmaliger kostenpflichtiger Erstellungsdialog, lokale exklusive Create-Sperre, status-before/status-after, automatischer Fallback nur bei positivem Kapazitätsmuster.
7. Historische Preis-Schätzungen ausdrücklich gekennzeichnet; keine falsche harte Kostenbegrenzung.
8. Snapshot/Deletion mit ausdrücklichen Bestätigungen, Backup-Hinweisen; keine automatische Wiederholung eines abgebrochenen Bootstrap.
9. Tests mocken alle externen Aufrufe, sodass keine Kosten und Downloads ausgelöst werden.

## Ungeklärte oder offene Risiken

- Keine unabhängige Prüfung durch zweite Person/Instanz dokumentiert. Dieses Dokument ist eine vorbereitete Checkliste und stellt **keine** unabhängige Freigabe dar.
- Die lokale Windows-Benutzeridentität besitzt Zugriff auf eigene Credential-Manager-Secrets; Malware unter derselben Identität kann diese lesen.
- Ein verwendetes GitHub-Repository ist eine Quelle für ausführbaren Remote-Bootstrap-Code. Repository-Berechtigungen und Branchschutz sind sicherheitskritisch. Der lokale Commit wird gezeigt, aber der bestehende Adapter lädt seinen pod_entry von main; das kann zwischen Synchronisierung und Start wechseln. Ein SHA-gepinnter, verifizierter Deployment-Pfad ist noch nicht implementiert.
- SSH-Trust-On-First-Use-Fingerprint muss wirklich unabhängig geprüft werden. Ungeprüfte Benutzerbestätigung wäre eine MITM-Gefahr.
- Remote-Prozessumgebung kann Secrets für denselben Linux-Benutzer sichtbar machen. Kein Remote Secret Store implementiert.
- Kein Fortsetzungsprotokoll für SSH-Abbruch; nach Verbindungsverlust muss die tatsächliche Remote-Tätigkeit überprüft werden.
- Kostenangebot ist nicht live. Ein zulässiger Schätzwert kann vom später abgerechneten Preis abweichen.
- Exportredaktion nutzt bekannte Tokens und Muster; unbekannte Secretformate können zusätzliche manuelle Redaktion erfordern.
- Keine authentifizierte Code-Signatur für den Windows-Installer; Hashdatei allein schützt nicht vor kompromittiertem Repository/Build. Updates werden deshalb nicht automatisch installiert.
- Kein echter Thunder-, Snapshot-, Downloader-, SSH- oder Windows-Installer-Livetest durchgeführt.

## Vor Merge verpflichtend

- Auf Windows erfolgreiche CI, kontrollierte Installerinstallation/Deinstallation, Git-Credential-Manager-Test.
- Zweite, unabhängig durchgeführte Sicherheitsprüfung aller Secret-, SSH-, Create- und Updatepfade, inklusive Negativtests.
- Statische Analyse ohne High-Severity-Funde, Tests der Race-/Fehlerfälle.
- Keine GPU-Livetests im Rahmen dieses Auftrags. Runtime-Risiken müssen verbleibend benannt werden.

