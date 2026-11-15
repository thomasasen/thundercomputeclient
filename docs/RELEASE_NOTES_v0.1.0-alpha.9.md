# Thunder Compute Client v0.1.0-alpha.9

**Geführter Workflow: Katalog laden → Workflow auswählen → Hardwareempfehlung prüfen → Pod erstellen → Pod verwalten. Alpha, nicht produktionsfreigegeben.**

## Neu

- **Ein klarer Ablauf in der Desktop-GUI:** „Katalog laden und Workflows wählen“ ruft den privaten Katalog ab und öffnet die Auswahl. Mehrfachauswahl und Dateikonflikte sind sichtbar; „Auswahl übernehmen → Hardware prüfen“ führt zur Hardwarekonfiguration.
- **Hardwareempfehlungen aus der echten Quelle:** Der Client liest `Persistent Volume (Empfehlung)` und die fünf empfohlenen RunPod-GPUs aus den privaten README-Tabellen pro Workflow. Empfehlungen sind an die authentifizierte GitHub-Revision gebunden; keine öffentliche Raw-URL und kein lokaler Checkout.
- **Nachvollziehbare Planung:** Deduplizierte Modellgrößen + 40 GB frei gewählte Planungsreserve für die gesamte Thunder-Disk, abgeglichen mit der dokumentierten RunPod-Volume-Referenz. VRAM-Orientierung basiert auf den jeweils gelisteten RunPod-GPU-Klassen. Bei Mehrfachauswahl wird der strengere Wert sichtbar.
- **Manuell veränderbar:** Mit „Planungswerte übernehmen“ wird zu kleiner Speicher erhöht. Bei vollständigen README-Angaben kann der Client GPU-Kandidaten rein nach VRAM-Klasse aktivieren/priorisieren. CPU, GPU-Anzahl, Template, Preisgrenze und tatsächliche Anbieterkompatibilität bleiben manuell.
- **Pod-Verwaltung nach Erfolg:** Nach bestätigter kostenpflichtiger Instanzerstellung öffnet sich automatisch „Pods verwalten“, mit Status, SSH, privatem ComfyUI, Logs/Bootstrap, Snapshots und Löschfunktion. Bereits bestehende Pods bleiben direkt erreichbar.
- **Sicherheit:** Kein automatischer kostenpflichtiger Schritt beim Navigieren oder Übernehmen von Empfehlungen. Unveränderte ausdrückliche Preisbestätigung, kein Retry bei uneindeutigem Create-Fehler.

## Wichtige Grenzen

Die README-Tabellen sind primär **RunPod-Hardwareempfehlungen**. Die VRAM-Orientierung ist **keine** gemessene Mindestanforderung für Thunder; GPU-Modelle, Treiber, CPU-/RAM-Bedarf und Verfügbarkeit sind nicht live validiert. Die Diskreserve von 40 GB ist ein konservativer Planungswert, kein gemessener Bedarf. Die angezeigten GPU-Stundentarife sind historische Schätzungen, **keine Live-Preise oder serverseitigen Kostenlimits**.

Die Windows-CI prüft die echte WPF-Oberfläche und sämtliche automatisierten Mock-/Regressionstests (einschließlich Hardware-Parser, Multi-Workflow-Deduplizierung, unbekannte Größen und Auswahlfolge). Kein echter Pod wurde für die Tests erstellt. Unsignierter Installer, unabhängiges Security-Review und vollständiger End-to-End-Test bleiben offen.
