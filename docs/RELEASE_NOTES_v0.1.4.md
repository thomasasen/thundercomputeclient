# Thunder Compute Client v0.1.4 – A100-Providerkennung und Kostenprüfung

## Fehlerbehebung

- **Fehler:** „Thunder-Spezifikation für GPU-Konfiguration fehlt: a100_x1“ in v0.1.3.
- **Ursache:** Die offizielle Thunder-CLI verwendet `a100xl` für A100 mit 80 GB und `a100xl_x1` für die Ein-GPU-Konfiguration. Im Client war `a100` als Providerkennung verwendet worden.
- **Korrektur:** Preisabfrage, Spezifikationsprüfung und `tnr create --gpu` normalisieren A100 zu `a100xl`; lokale GPU-Auswahl und gespeicherte `a100`-Reihenfolgen bleiben kompatibel.
- **Preiskorrektur:** Die A100-Speicherkalkulation berücksichtigt bei 8 vCPUs konservativ vier zusätzliche vCPUs gemäß offizieller CLI-Testkonfiguration; neuer Richtwert für frische Konfigurationen 1,35 USD/h. Gespeicherte Limits werden nicht automatisch überschrieben.
- **Qualität:** Tests verwenden die offiziellen `a100xl_x1`-Spezifikations- und Preisschlüssel, prüfen fehlende Anbieter-Spezifikationen und erhöhte Live-Tarife sowie den tatsächlichen `tnr create`-Argumentvektor mit Mock-CLI.

## Grenzen und Sicherheit

Kein kostenpflichtiger Pod-Livetest. Die Preisberechnung vor Create verwendet ausschließlich bestätigte API-Antworten als untere Schranke gegenüber lokalen Listenwerten; bei fehlenden Spezifikationen, unzulässigen vCPUs oder fehlenden Preisen bleibt die Erstellung gesperrt. Höhere Stundenpreise benötigen eine ausdrückliche Freigabe. Keine serverseitige Abrechnungsbremse.
