# Thunder Compute Client v0.1.3 – MiniMax H3 GPU- und Preisplanung

## Behoben

- Der MiniMax-H3-Image-Workflow mit 80-GB-VRAM-Orientierung wird nicht mehr fälschlich allein durch eine deaktivierte A100 und die für 48-GB-Karten passende alte Standardpreisgrenze ausgeschlossen.
- A100 (80 GB, 8 vCPUs) ist regulär aktiviert. H100 (80 GB, 4 vCPUs) ist als kostspielige, explizit zu aktivierende Option ergänzt.
- Neue Installationen beginnen mit einer 1,25-USD/h-Schätzpreisgrenze. Bereits gespeicherte Limits werden **nicht still geändert**: „Planungswerte übernehmen“ bietet nach expliziter Bestätigung eine Anpassung an.
- Thunder-spezifische 80-GB-Planungswerte werden getrennt von RunPod-Rankings ausgewertet. Fehlermeldungen zeigen konkrete Ausschlussgründe und die geplante Diskgröße.
- README und Benutzerhandbuch beschreiben den 211-GB-Diskplan für alle 11 Modellgewichte und die Grenzen der 48-GB-Varianten.

## Qualität und Grenzen

Offline-Regressionen für Thunder-README-Metadaten, A100-Auswahl, Kostenlimit und Diskplanung. Windows-CI: .NET-Testlauf, WPF-Smoke-Test und Installer; Linux-CI: SSH-Supervisor-Fixtures. **Keine bezahlte Live-GPU-/ComfyUI-Inferenzvalidierung** durch diese Tests. Preisabfragen des Thunder-Anbieters, Zugangsdaten, verfügbarer A100-Bestand und Lizenzfreigaben bleiben Voraussetzungen. Alle Pods werden erst nach separater GUI-Freigabe kostenpflichtig erstellt. Keine serverseitige Ausgabenbegrenzung, keine Garantien für 8 MP und keine automatische Löschung bestehender Pods.
