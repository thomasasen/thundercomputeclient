# Thunder Compute Client v0.1.0-alpha.13

**Ein-Klick-Bereitstellung aus privaten ComfyUI-Workflow-Manifeste. Alpha-Preview, keine Produktionsfreigabe.**

## Neuer Ablauf

1. Katalog laden, einen oder mehrere Workflows auswählen.
2. **„Automatisch bereitstellen: Pod + ComfyUI + Modelle …“** (alternativ auf der Hardwareseite) anklicken. Der Client übernimmt die aus den gewählten Manifesten berechnete Speicherplanung und prüft bereits aktivierte GPU-Kandidaten anhand der README-VRAM-Orientierung und der vorhandenen lokalen Schätzpreisgrenze.
3. Den kostenpflichtigen Create-Versuch ausdrücklich bestätigen. Existiert genau ein Pod, wird er nur nach weiterer Zustimmung wiederverwendet. Keine neuen Pods bei unklaren Fehlern oder mehreren vorhandenen Instanzen.
4. Automatisch auf RUNNING/SSH-Port warten, den offiziellen Thunder-Key-Schritt ausführen, einmaligen neuen ED25519-Fingerprint unabhängig prüfen und bestätigen.
5. Bestehendes privates `thunder/bootstrap.sh` starten. Der Upstream führt die frühen parallelen Modell- und Custom-Node-Downloads mit `MODEL_DOWNLOAD_WORKERS=4` aus (maximal vier adaptive Datei-Jobs), installiert ComfyUI und meldet Downloadfortschritt/HTTP-Bereitschaft in **Downloads & Logs**.
6. ComfyUI über den privaten SSH-Tunnel in **Instanzen** öffnen.

**Die Kostenfreigabe zeigt** die konkrete GPU-Reihenfolge, vCPUs, Disk, Modellgröße und historische Schätzungen für 1/4/12 Stunden. Preise und GPU-Kompatibilität sind keine Thunder-Livemessungen. Fehlende Manifestgrößen, kollidierende Datei-Ziele, zusätzliche Manifestautorisierungen, ungeeignete aktivierte GPUs und nicht vorhandene Civitai-Zugangsdaten bei Civitai-Modellen blockieren die automatische Vollinstallation frühzeitig.

## Sicherheits- und Funktionsgrenzen

Der Remote-Bootstrap geht am Ende direkt in den ComfyUI-Prozess über. **Die Windows-GUI muss in dieser Version während der Bereitstellung geöffnet bleiben; ein persistenter Background-Supervisor mit sicherem Auto-Resume ist noch nicht implementiert.** Bei Verbindungsabbruch oder Fehlern wird kein weiterer kostenpflichtiger Pod erzeugt, keine Installation blind wiederholt und der bestehende Pod nicht automatisch gelöscht. „Automatik abbrechen“ stoppt die Verbindung, **nicht** den kostenpflichtigen Pod.

Ein bereits laufender ComfyUI-Webdienst wird erkannt und nicht durch einen zweiten Bootstrap gestartet; der Client bestätigt dadurch noch **nicht**, dass andere ausgewählte Workflows vollständig installiert sind. Der bestehende Remote-Entrypoint wird weiterhin von beweglichem `main` geladen und ist nicht vollständig commit-gepinnt. Ein unbekannter SSH-Hostfingerprint muss weiterhin unabhängig bestätigt werden.

## Qualitätssicherung

Windows-CI prüft die WPF-App, den echten Start aller acht Views, den Installer mit SHA256SUMS sowie neue reine Mock-Regressionstests für die automatisierte Hardwareplanung, Kostenfilterung, das Erkennen vorhandener/fehlender Pods und das begrenzte Warten auf einen gültigen SSH-Endpunkt. **Kein echter Pod wurde während der CI erstellt oder kostenpflichtig betrieben.** Keine unabhängige Security-Freigabe, kein Code Signing, kein Live-End-to-End-Test mit echten Tokens/Downloads. Draft-PR bleibt offen.
