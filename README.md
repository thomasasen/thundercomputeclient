# Thunder Compute Client

Nativer Windows-Desktop-Client (.NET 10, WPF) für die ComfyUI-Workflows im **privaten** Repository thomasasen/runpod-ai-workflows. Der Client ruft die offizielle Thunder-CLI (tnr) auf, synchronisiert Manifestdaten mit Git, erstellt Instanzen nach ausdrücklicher Bestätigung und startet den bestehenden Thunder-Bootstrap über eine abgesicherte SSH-Verbindung.

**Status: Alpha-Vorabversion v0.1.0-alpha.1, nicht produktionsfreigegeben.** Build und mockbasierte Tests werden in Windows GitHub Actions geprüft. Es gibt keine bestätigten Thunder-GPU- oder Modelldownload-Livetests. Die Anwendung ist nicht als produktiv validiert oder vollumfänglich fertig zu verstehen; siehe docs/IMPLEMENTATION_STATUS.md.

## Voraussetzungen

- Windows 11 x64, .NET selbstenthaltenes Installationspaket; Windows 10 nicht getestet.
- Offizielle Thunder Compute CLI (tnr), angemeldet über tnr login.
- Git for Windows inklusive Git Credential Manager, für das private Workflow-Repository mit Lesezugriff angemeldet.
- OpenSSH-Client, ssh-keygen und ssh-keyscan unter Windows.
- GitHub fine-grained PAT mit Contents: Read-only nur auf runpod-ai-workflows, separat für den **Remote-Bootstrap** im Windows Credential Manager gespeichert. Hugging Face und Civitai bei Bedarf ebenfalls.
- Vertrauenswürdiger ED25519-SSH-Hostfingerprint aus einer unabhängigen Thunder-Quelle zum Abgleich vor dem ersten SSH-Zugriff.

## Kompilieren

Auf Windows mit .NET 10 SDK:

    dotnet restore tests/ThunderClient.Tests/ThunderClient.Tests.csproj
    dotnet test tests/ThunderClient.Tests/ThunderClient.Tests.csproj -c Release
    dotnet publish src/ThunderClient/ThunderClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish

Der GitHub-Actions-Workflow erzeugt aus dem Publish-Ordner ein per-user Inno-Setup-Installationspaket, SHA256SUMS und Testberichte. Der erste GitHub-Release ist ausdrücklich als Alpha-Vorabversion gekennzeichnet; stabile Releases bleiben bis zum Sicherheitsreview und Windows-Smoke-Test gesperrt. Die App installiert Updates **nicht** selbst.

## Nutzung

1. tnr installieren und anmelden; Git/GCM für das private Repository authentifizieren.
2. Client starten. Er klont beim ersten Start das Workflow-Repository und prüft danach bei jedem Start main; lokale Änderungen werden nicht überschrieben.
3. Unter Workflows einen oder mehrere Einträge auswählen und unter Neue Instanz GPU-Prioritäten, vCPU-Zahlen, Anzahl GPUs, Disk, Template und Preisgrenze prüfen.
4. Vor dem kostenpflichtigen Erstellen wird eine Zusammenfassung mit geschätzten Tarifen angezeigt. Die Zustimmung ist zwingend.
5. Unter Instanzen die laufende Instanz wählen, den SSH-Hostfingerprint mit Thunder abgleichen, dann unter Downloads & Logs den vorhandenen Bootstrap starten.
6. ComfyUI ausschließlich über den privaten SSH-Loopback-Tunnel öffnen. Vor dem Löschen eine abgeschlossene Datensicherung/Snapshot verifizieren.

Weder Workflows noch Modelldateien werden in dieses Repository kopiert. Die Bootstrap-/Modelldownloadlogik verbleibt ausschließlich in runpod-ai-workflows.

Weitere Details: docs/USER_GUIDE.md, docs/ARCHITECTURE.md, docs/SECURITY_REVIEW.md und docs/IMPLEMENTATION_STATUS.md.

