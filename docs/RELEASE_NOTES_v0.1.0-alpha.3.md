# Thunder Compute Client v0.1.0-alpha.3

**Alpha-Vorabversion für Windows 11 x64. Keine Produktionsfreigabe.**

## Neue Oberfläche

Vollständige Überarbeitung der Windows-Oberfläche nach den Gestaltungsgrundsätzen des [Apple-Design-Skills](https://github.com/emilkowalski/skills/blob/main/skills/apple-design/SKILL.md), für natives WPF angepasst:

- Permanente, gut lesbare Seitenleiste für alle sieben Funktionsbereiche
- Neues Dashboard mit Instanz-, Workflow-, Kosten- und Verbindungsstatus
- Neu gestaltete Inhaltskarten, Tabellen, Eingabefelder, Zugangsdaten und Ressourcenkonfiguration
- Hell-/Dunkelmodus mit konsistenten Farben, Hover-/Pressed- und Keyboard-Fokus-Rückmeldung
- Gleichbleibende Kostenbestätigung, ohne automatische kostenpflichtige Aktionen

## Fehlerbehebungen

- Git-Clone-Fehler wird nicht mehr durch `Access to the path pack-*.idx is denied` beim Aufräumen überdeckt.
- Lokaler exklusiver Sync-Lock verhindert gleichzeitiges Klonen und Fetch durch zwei Instanzen.
- Git, Thunder-Status und Snapshots werden beim Start unabhängig initialisiert. Scheitert Git, bleibt die Oberfläche bedienbar und zeigt den Git-Fehler in einer eigenen Statuskarte.
- Testabdeckung erweitert auf **18 Unit-/Regressionstests**; die Windows-CI öffnet und rendert **alle sieben Ansichten** ohne reale GPU-/Netzwerkoperationen.

## Hinweise für vorhandene Nutzer

Installationspaket über die vorhandene `alpha.2`-Installation installieren. Einstellungen und separat gespeicherte WinCred-Einträge sollen bestehen bleiben; tatsächliche Installation/Deinstallation auf einem Endbenutzergerät wurde nicht automatisiert getestet.

Bei einem weiterhin fehlerhaften Git-Checkout: [Fehlerdiagnose](https://github.com/thomasasen/thundercomputeclient/blob/feature/windows-desktop-client/docs/TROUBLESHOOTING.md). Den Zugriffsfehler auf dem konkreten Rechner können wir ohne Geräteprotokoll nicht als endgültig behoben bestätigen.

## Bekannte Grenzen

Der Installer ist nicht codesigniert; unabhängiges Red-Team-Review und vollständiger Thunder-/GPU-/SSH-End-to-End-Test fehlen. Preise sind Schätzungen und keine Abrechnungsgrenzen. Windows 10 nicht validiert. Der ursprüngliche Draft-PR bleibt offen, `main` unverändert. Details: [Issue #2](https://github.com/thomasasen/thundercomputeclient/issues/2).
