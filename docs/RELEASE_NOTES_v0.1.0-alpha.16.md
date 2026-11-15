# Thunder Compute Client v0.1.0-alpha.16

**Hotfix: klare Diagnose bei Thunder-API-Timeout während der SSH-Schlüsselregistrierung. Weiterhin Alpha, kein Stable-Release.**

## Reales Fehlerbild
Pod `0` (L40) wurde angelegt und meldete `RUNNING`, aber `tnr connect 0 --json` scheiterte mit:
```
failed to add SSH key: transport error: ... Post
"https://api.thundercompute.com:8443/v1/instances/0/add_key":
context deadline exceeded
```
Die offizielle Thunder-CLI hat ein HTTP-Zeitlimit von 30 Sekunden. Das ist **kein lokaler SSH-Port-/ComfyUI-Portfehler**, und es ist **nicht sicher**, dass der POST serverseitig überhaupt nicht bearbeitet wurde.

## Änderungen
- Spezifische, strukturierte Fehlerklassifikation für `add_key`-Timeouts (nicht für andere 401/403-/SSH-Fehler); **kein** automatisches erneutes `tnr connect`/`add_key`, keine zweite GPU-Instanz.
- Verständlicher GUI-Hinweis, dass der Pod weiterhin Kosten verursachen kann und die Installation **vor** Bootstrap/Downloads gestoppt wurde.
- Vor expliziter Wiederverwendung des vorhandenen Pods Hinweis, dass erneuter `tnr connect` bei unbekanntem POST-Resultat möglicherweise einen weiteren Schlüssel registriert.
- In **Downloads & Logs** direkte Buttons zur offiziellen Thunder-Konsole und zur Thunder-Dienststatusseite.
- Neue Regressionstests für exakte CLI-JSON-Antwort, andere Fehlerklassen sowie **genau einen** externen Connect-Versuch im Fehlerfall.

## Vorgehen mit vorhandener Instanz
Bei genau einem Pod unter **Instanzen** den Zustand prüfen. **Keinen neuen Pod erstellen.** Nach Prüfung der Thunder-API kann die Verbindung ausdrücklich erneut versucht werden, ohne neue Instanz. Wiederholte 30s-`add_key`-Timeouts an [Thunder-Support](https://www.thundercompute.com/contact) melden. Ein bestehender kostenpflichtiger Pod muss bei Nichtnutzung nach Prüfung von Backups/Snapshots separat gelöscht werden.

## Grenzen
**Der externe Thunder-API-Timeout ist durch eine Änderung dieses Windows-Clients nicht zuverlässig behebbar.** Diese Version verbessert die Fehlerursache, sicheren Umgang und Bedienung, nicht den Backend-Endpunkt selbst. Weder die reale SSH-Schlüsselanlage noch ein vollständiger ComfyUI-/GPU-/Modelldownload-Live-Test ist durch CI bestätigt. Frühere Red-Team-P0/P1-Findings (Kosten/Resume/Hardwareprüfung) bleiben offen. Nicht signierter Installer. Feature-PR bleibt Draft; `main` unverändert.
