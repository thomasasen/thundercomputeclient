# Thunder Compute Client v0.1.0-alpha.10

**SSH-Verbindungsdiagnose für Windows 11 x64. Alpha-Vorabversion, keine Produktionsfreigabe.**

## Behebung und Diagnose

- Die Fehlermeldung „SSH-Hostschlüssel konnte nicht abgefragt werden“ wurde verbessert. Der Client zeigt nun nach drei begrenzten Scanversuchen den angesprochenen **Host und Port**, den `ssh-keyscan`-Exitcode und einen gekürzten Diagnoseauszug statt eines nichtssagenden generischen Fehlers an.
- Bei frisch gestarteten Pods wird `ssh-keyscan` mit einer kurzen Pause wiederholt. Es wird dabei **weder ein Pod erstellt noch ein Bootstrap ausgelöst**.
- Unter Windows bevorzugt die Anwendung die nativen OpenSSH-Werkzeuge aus `Windows\\System32\\OpenSSH`, sofern vorhanden.
- Die Host/Port-Kombination und das binäre ED25519-Keyformat werden vor Übernahme in `known_hosts` validiert. Es findet weiterhin **kein automatisches Vertrauen** in einen neuen SSH-Host statt; der Fingerprint muss unabhängig verifiziert und bestätigt werden.
- `StrictHostKeyChecking=yes` bleibt aktiv.

**Wichtig:** Die Änderung verbessert vor allem das Verhalten bei verzögertem SSH-Start und die Diagnose. Sie kann einen gesperrten SSH-Port, ein falsches Thunder-Ziel, einen nicht startenden SSH-Dienst oder einen Server ohne ED25519-Schlüssel **nicht** beheben.

## Prüfung auf dem eigenen Rechner

Falls die Verbindung weiterhin scheitert, **Downloads & Logs → Log kopieren** verwenden und die genaue Fehlermeldung prüfen. Zudem in PowerShell:

```powershell
Get-Command ssh-keyscan
tnr status --json --no-wait
```

IP und Port nur aus der aktuellen Verbindungsauskunft von Thunder entnehmen. Vor der Weitergabe persönliche Netzwerkdaten und Geheimnisse entfernen. Keine privaten Schlüssel, Zugangstokens oder ungeprüften Hostkeys teilen.

## Freigabestatus

Windows-GitHub-Actions: vorhandene Regressionstests sowie zusätzliche SSH-Hostkey-Tests, echter WPF-GUI-Starttest, Installer und SHA256SUMS. Kein kostenpflichtiger Pod, kein Live-SSH-Zugriff in CI. Installer noch nicht codesigniert; unabhängiger Sicherheitstest und vollständige Thunder-/SSH-End-to-End-Prüfung stehen aus. Draft-PR bleibt offen, main unverändert.
