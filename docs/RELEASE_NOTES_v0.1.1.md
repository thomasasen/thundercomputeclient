# Thunder Compute Client v0.1.1 – SSH-Hostprüfung und Diagnose

**Reguläres Windows-x64-Hotfix-Release** auf Basis von v0.1.0.

## Behobener Fehler

- Die GUI zeigte bisher pauschal **„SSH-Hostschlüssel nicht bestätigt“**, auch wenn in Wirklichkeit `ssh-keyscan`, `ssh.exe` oder `ssh-keygen` technisch fehlgeschlagen war. Jetzt zeigt sie die konkrete, vorab bereinigte Fehlermeldung (maximal 1.200 Zeichen) und verweist auf das vollständige bereinigte Log.
- Wurde der SHA256-Hostfingerprint nicht bestätigt, wurde die dadurch ausgelöste `OperationCanceledException` im Auto-Deploy fälschlich als interner Timeout umgedeutet. Eine eigene Exception erhält die echte Ursache: Benutzer hat die Host-Identität nicht freigegeben.
- Der Fingerprint-Dialog erklärt jetzt den Unterschied zwischen einem über das Netz abgefragten **unvertrauten** Hostschlüssel und der unabhängig ermittelten Hostidentität. Er nennt als Prüfweg `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` **ausschließlich über eine unabhängig vertrauenswürdige Provider-/VM-Konsole**, falls vorhanden. Ein zweiter `ssh-keyscan` ist ausdrücklich kein unabhängiger Nachweis.
- Regressionstests unterscheiden Fingerprint-Nichtbestätigung, technische OpenSSH-/Scan-/Zeitlimitfehler und andere Fehler.

## Betrieb mit vorhandenem Pod

1. Windows-Client schließen und `ThunderClient-Setup-0.1.1-win-x64.exe` installieren.
2. Unter **Instanzen** den vorhandenen Pod prüfen. **Keinen zweiten Pod erstellen**; der bereits vorhandene Pod kann Kosten verursachen.
3. Mit dem bestehenden Pod fortfahren. Einen unbekannten Fingerprint nur bestätigen, wenn sein SHA256-Wert über einen *unabhängigen, vertrauenswürdigen* Zugang exakt verifiziert wurde. Ist dieser nicht verfügbar, Thunder-Support fragen.
4. Bei technischem SSH-Fehler den nun konkret angezeigten Grund und das bereinigte Log aus **Downloads & Logs** zur Fehlersuche verwenden. Keine Tokens, privaten Schlüssel oder vollständigen internen Konfigurationen weitergeben.

## Validierung und Grenzen

- Windows CI: .NET-Tests inkl. neuer Fehlerklassifizierung, WPF-GUI-Smoke-Test, Installer und SHA-256.
- Linux CI: isolierte Remote-Supervisor-Tests für Detach und Reattach.
- **Kein Nachweis einer echten Thunder-/GPU-/ComfyUI-End-to-End-Installation oder erfolgreicher Bildgenerierung.** Dieses Release repariert die Diagnose und die korrekte Behandlung einer nicht erteilten Hostfreigabe. Es umgeht die Hostprüfung nicht und kann Thunder-seitige SSH- oder Netzprobleme nicht selbst beheben.
- Installer weiterhin **nicht codesigniert**, keine serverseitige Kostensperre. Unabhängiges Security-Review weiterhin offen.
