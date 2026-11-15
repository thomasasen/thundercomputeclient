# Thunder Compute Client v0.1.0-alpha.15

**SSH-Timeout-Diagnose-Hotfix; weiterhin Alpha, keine stabile Freigabe.**

## Problem aus dem Nutzerprotokoll

Ein Thunder-Pod wurde erfolgreich erstellt, war bereits `RUNNING` und hatte einen SSH-Endpunkt. Vor Bootstrap und Modelldownload meldete der Client nach ca. 52 Sekunden nur `A task was canceled`. Die Meldung stammte aus der Verarbeitung zeitbegrenzter Teilprozesse; **welcher Teilprozess konkret betroffen war, ist aus dem alten Log nicht sicher zu ermitteln**. Dieser Hotfix führt deshalb genauere Diagnosen ein, statt eine nicht belegte Netzwerkursache zu behaupten.

## Änderung

- `ProcessRunner.RunAsync` unterscheidet zwischen dem **internen Prozess-Zeitlimit** und einem **ausdrücklichen Abbruch**: Bei Fristablauf wird `TimeoutException` mit Befehlname und Sekunden ausgegeben.
- Mehrstufiges Statusprotokoll: Thunder-SSH-Endpunkt, gespeichertes Hostvertrauen, einzelne Keyscan-Versuche, isolierter `ssh.exe`-Hostkey-Fallback, Fingerprintprüfung und echter Anmeldetest. So bleibt bei einem Abbruch der letzte erreichte Schritt sichtbar.
- `ssh-keyscan`-Fristabläufe werden kontrolliert abgefangen und wiederholt. Beim Windows-sntrup-KEX-Fallback darf ein bereits in der **temporären, isolierten** Datei erfasster öffentlicher ED25519-Schlüssel auch nach dem `ssh -N`-Timeout geprüft werden. Die Datei wird gelöscht.
- Unerwartete `OperationCanceledException` ohne tatsächlich abgebrochenen Bereitstellungs-Token werden als Zeitlimit beim letzten protokollierten Schritt behandelt.
- **Keine Reduktion der SSH-Hostvertrauensprüfung:** Kein automatisches Akzeptieren unbekannter Fingerprints. Reguläres SSH mit `StrictHostKeyChecking=yes`. Keine automatische zweite kostenpflichtige Instanz nach Fehlschlag.

## Installation / Wiederverwendung

Vorhandenen kostenpflichtigen Pod unter **Instanzen** auf Status prüfen und bei genau einer Instanz **„Bestehenden Pod automatisch einrichten“** nach Zustimmung benutzen. Bootstrap und parallele Modelldownloads beginnen erst nach erfolgreicher SSH-Prüfung. Bei weiterem Fehler **Downloads & Logs → Log kopieren** verwenden und vor dem Teilen Geheimnisse entfernen.

## Grenzen

Automatisierte Windows-Tests und GUI-Smoke ersetzen keinen Live-SSH-Handshake. Die im [Red-Team-Review](https://github.com/thomasasen/thundercomputeclient/blob/feature/windows-desktop-client/docs/RED_TEAM_AUDIT_2026-11-15.md) dokumentierten P0/P1-Blocker, insbesondere unvollständige live Kostengrenze und fehlender Remote-Resume-Supervisor, bleiben offen. PR bleibt Draft; `main` unverändert. Keine kostenpflichtige Instanz durch diese Änderung erzeugt.
