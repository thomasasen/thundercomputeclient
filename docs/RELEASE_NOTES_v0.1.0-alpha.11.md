# Thunder Compute Client v0.1.0-alpha.11

**Neue Systemstatus- und Einrichtungsansicht für Windows 11 x64. Alpha, keine Produktionsfreigabe.**

## Neuer Screen: Systemstatus

- Achte Seite **Systemstatus** in der Seitenleiste sowie Schnellzugriff auf der Übersicht.
- **Jetzt alles sicher prüfen** führt ausschließlich lesende Tests aus, keine Pod-Erstellung, kein SSH-Auto-Trust und keine Modelldownloads.
- Sieben prüfbare Bereiche: **privater GitHub-API-Zugriff**, **Thunder-CLI und Anmeldung**, **Windows OpenSSH**, **lokaler privater SSH-Key des laufenden Pods**, **SSH-Hostvertrauen und tatsächliche Anmeldung**, **Hugging Face** (optional), **Civitai** (optional).
- Status **Erfolgreich getestet**, **Vorhanden**, **Fehlt**, **Einrichtung nötig**, **Fehler**, **Nicht prüfbar** oder **Optional · fehlt**. Jede Karte zeigt den letzten Prüfzeitpunkt und eine konkrete Erklärung, ohne Geheimnisse auszulesen oder anzuzeigen.
- **Offizielle Einrichtungslinks** und direkter Sprung zum richtigen Bereich im Client. GitHub Fine-grained PAT mit `Contents: Read-only` nur für `runpod-ai-workflows`; Thunderbird-CLI `tnr login`; Microsoft OpenSSH; Hugging Face/Civitai-Keyformulare.
- Erfolgreicher GitHub-Check bestätigt einen echten privaten Manifest-Abruf. Thunder wird mit `tnr status --json --no-wait` geprüft. HF und Civitai werden über lesende Konto-API-Endpunkte getestet; dies garantiert **nicht** den Zugriff auf einzelne Gated-Modelle.
- **SSH:** Ohne laufenden Pod und vorhandene lokale Schlüsseldatei bleibt der Test „Nicht prüfbar“. Nur bei zuvor unabhängig bestätigtem Hostfingerprint testet der Client über StrictHostKeyChecking=yes die SSH-Anmeldung. Keine automatische Hostschlüsselbestätigung.
- Gespeicherte API-Tokens verlassen den Windows Credential Manager ausschließlich für die vom Benutzer gestarteten HTTPS-Anfragen an den jeweiligen Anbieter, in Authorization-Headern statt in URLs. Weder Tokeninhalte noch private SSH-Schlüssel werden angezeigt.

## Verifikation

Windows-GitHub-Actions prüft die .NET-10-WPF-App, den tatsächlichen Start aller **acht Ansichten**, zusätzliche Mock-Regressionstests und den Installer samt SHA256SUMS. Es werden keine echten Provider-Tokens, GPU-/SSH-Kostenaktionen oder Downloads in CI ausgelöst.

**Grenzen:** Statusprüfung ist eine Momentaufnahme, keine kontinuierliche Überwachung und kein vollständiges Security-Attest. Installer nicht codesigniert. End-to-End-Test mit echten privaten Tokens/Thunder-Pod und unabhängiger Security-Review weiterhin erforderlich. Der Draft-PR bleibt offen, `main` unverändert.
