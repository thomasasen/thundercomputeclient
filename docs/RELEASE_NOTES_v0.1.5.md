# Thunder Compute Client v0.1.5 – Read-only SSH-Timeout-Wiederholung

## Behoben

Bei einem frisch erstellten A100-Pod in v0.1.4 wurde der SSH-Hostschlüssel erfolgreich mit unabhängig bestätigtem ED25519-Fingerprint gespeichert. Der **erste** Authentifizierungsversuch `ssh -T ubuntu@... true` lief aber nach 25 Sekunden in ein Prozess-Timeout und beendete trotz später erfolgreicher Remote-Verbindung den Ein-Klick-Start. Eine bestehende Retry-Schleife behandelte nur Exitcodes, nicht geworfene Timeout-Ausnahmen.

v0.1.5 behandelt jetzt `TimeoutException` und **nur interne** `OperationCanceledException` als temporäre, fehlgeschlagene SSH-Authentifizierungsproben. Eine echte Nutzer-Cancellation bleibt sofort wirksam. Die Anzahl der Proben bleibt wie bisher auf maximal sechs mit Pausen begrenzt. Die Fehlermeldung nennt beim endgültigen Fehlschlag die konkrete SSH-Ursache und verweist auf den bereits existierenden Pod.

## Sicherheit / Regression

Jeder Probe-Retry verwendet ausschließlich `ssh true` mit `BatchMode=yes` und `StrictHostKeyChecking=yes`. **Keine** erneute Schlüsselregistrierung, **keine** Host-Key-Autofreigabe, **keine** zweite Instanzerstellung. Hostkey-Wechsel führen weiterhin zum sofortigen Abbruch. Die Regressionstests decken Prozess-Timeouts, interne Command-Deadlines, begrenzte Wiederholungen, echte Nutzer-Cancellation sowie bestehende Hostkey-Tests ab.

## Grenzen

Keine Änderung am bereits laufenden Remote-Job und keine kostenpflichtige GPU-Inferenzvalidierung. Der Ein-Klick-Pfad wird offline geprüft; Netzwerk- und GPU-Verfügbarkeit bleiben anbieterseitig. Laufende Pods verursachen bis zur bewussten Beendigung Kosten.
