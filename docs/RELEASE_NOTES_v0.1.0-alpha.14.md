# Thunder Compute Client v0.1.0-alpha.14

**Windows-SSH-Hostschlüssel-Hotfix für bereits laufende Pods. Weiterhin Alpha, keine Stable-Freigabe.**

## Behebung: `ssh-keyscan` / sntrup-KEX

Bestimmte mit Windows gelieferte OpenSSH-`ssh-keyscan`-Versionen schlagen bei Ubuntu/OpenSSH-Servern mit `choose_kex: unsupported KEX method sntrup761x25519-sha512@openssh.com` fehl (upstream: [Win32-OpenSSH #2140](https://github.com/PowerShell/Win32-OpenSSH/issues/2140)). In einer Nutzersitzung hatte die App bereits Pod `0` erstellt, aber vor SSH-Fingerprint/Bootstrap/Modelldownload abgebrochen.

Der Client erkennt jetzt genau diesen Fehlertyp und führt einen **isolierten, nicht authentifizierten `ssh.exe`-Handshake** aus. Der erhaltene öffentliche ED25519-Hostkey wird nur in einer temporären, anschließend gelöschten known_hosts-Datei gehalten. Keine privaten SSH-Keys, keine Anmeldung, kein Remote-Command. Der Nutzer muss weiterhin **unabhängig** den Fingerprint verifizieren und ausdrücklich bestätigen. Erst dann darf die reguläre Verbindung mit `StrictHostKeyChecking=yes` starten. Auch wenn der Fallback scheitert, wird kein neuer kostenpflichtiger Pod angelegt.

Die GUI erläutert diese SSH-Fehler jetzt kurz mit Pod-Kostenhinweis und konkreten nächsten Schritten; vollständige Fehlerdetails bleiben kopierbar in **Downloads & Logs**.

## Wiederaufnahme eines vorhandenen Pods

Unter **Instanzen** prüfen, ob der Pod noch läuft und ggf. Kosten verursacht. Für die einzige vorhandene Instanz **„Bestehenden Pod automatisch einrichten“** wählen. Die App fordert eine neue Zustimmung zur Verwendung an und legt keinen zweiten Pod an. Wer den Pod nicht weiter benötigt, kann ihn nach separater Bestätigung bewusst löschen.

## Tests und Grenzen

Windows-CI: WPF-Build, Mock-Regression für die exakte sntrup-KEX-Fehlermeldung, erzwungener nicht authentifizierter SSH-Handshake nur mit ephemerem Trust-Speicher, fehlender Hostkey führt zu Abbruch, GUI-Smoketest aller acht Ansichten, Installer + SHA256SUMS.

**Nicht durch CI nachgewiesen:** echte SSH-Verbindung zu deinem Thunder-Pod, Modell-Downloads, GPU-Kompatibilität, vollständiger Bootstrap/Resume. Die im [Red-Team-Audit](docs/RED_TEAM_AUDIT_2026-11-15.md) dokumentierten P0/P1-Releaseblocker (unvollständige Live-Kostenrechnung, Remote-Supervisor/Resume, etc.) bleiben bestehen. Nicht signierter Installer; PR bleibt Draft, `main` unverändert. Keine Test-Pods/GPU-Kosten wurden durch diese Änderung ausgelöst.
