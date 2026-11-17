# Projektregeln: Thunder Compute Desktop Client

## Geltungsbereich
Dieses Repository ist ein nativer Windows-Client für das unveränderte externe Repository thomasasen/runpod-ai-workflows. Die Workflowdaten kommen nur aus dem lokalen, authentifiziert synchronisierten Checkout. Niemals Manifest-, Modell- oder Installationslogik duplizieren.

## Architektur
- .NET 10, WPF und MVVM; UI und Core-/Infrastrukturklassen trennen.
- Externe Prozesse und Netzwerkaufrufe nur asynchron, mit Fehlerprüfung und Abbruchmöglichkeit.
- Thunder über dokumentierte tnr-CLI-JSON-Ausgaben anbinden, keine erfundenen Endpunkte.
- Neue Instanzen und andere kostenpflichtige oder destruktive Aktionen benötigen eine explizite GUI-Bestätigung.
- Automatischer GPU-Fallback nur bei exakt erkanntem Kapazitätsfehler, nach erneuter erfolgreicher Instanzprüfung.
- Keine Remote-Downloads in Tests; alle externen Schnittstellen mocken.
- Keine Geheimnisse in Git, Konfigurationsdateien, Prozessargumenten, URLs, Exportlogs oder Fehlermeldungen.
- SSH-Hostschlüssel niemals ungeprüft akzeptieren.
- Keine öffentliche ComfyUI-Portfreigabe, nur Loopback-Tunnel.
- Alle verfügbaren Aktionen müssen tatsächlich verdrahtet sein; nicht unterstützte Funktionen sichtbar erläutern.
- Keine Änderung am Repository runpod-ai-workflows aus diesem Projekt.

## Qualität
Vor einem Merge: Windows-CI (build + Tests), Review der Tests, manuelle statische Sicherheitssichtung gemäß docs/SECURITY_REVIEW.md. Kein kostenpflichtiger Thunder-Livetest im Entwicklungsauftrag. Blockierende Lücken offen dokumentieren.

