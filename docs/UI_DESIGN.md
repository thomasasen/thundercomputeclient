# Oberflächendesign – Thunder Compute Client

## Ziel

Die Anwendung ist ein Windows-Desktop-Arbeitsplatz für technisch anspruchsvolle, teilweise kostenpflichtige Thunder-/ComfyUI-Aufgaben. Sie muss zuverlässig, lesbar und vorhersehbar sein. Gestaltung darf keine angebliche Hardwareverfügbarkeit oder Abrechnungssicherheit suggerieren.

## Referenz und Anpassung

Referenz: [emilkowalski/skills – apple-design/SKILL.md](https://github.com/emilkowalski/skills/blob/main/skills/apple-design/SKILL.md), abgerufen am 15.11.2026. Die Quelle beschreibt weborientierte Interaktion und Apples Gestaltungsprinzipien; hier werden **Prinzipien** in native WPF-Steuerelemente übersetzt. Die Anwendung wird nicht als Apple-Anwendung oder macOS-Port ausgegeben.

1. **Orientierung:** Eine feste Seitenleiste mit sieben klar benannten Zielen ersetzt die schwer lesbare Tab-Leiste. Aktueller Bereich ist markiert; jeder Bereich hat einen eigenständigen Seitentitel.
2. **Sofortiges Feedback:** Hover, Pressed- und Keyboard-Focus-Zustände; eine dauerhafte Statuszeile und eine GitHub-API-Verbindungsstatus. Kostenrelevante Aktionen behalten ihre explizite Bestätigung.
3. **Hierarchie:** Hintergrund, Sidebar, Inhaltsfläche und Eingabefeld sind über wenige konsistente Farbebenen getrennt. Karten mit 16px-Radius und großzügigem Abstand, ohne dekorative Überlagerungen.
4. **Typografie:** Native Segoe UI, große Seitenüberschriften, niedrigere Abschnittstitel, hervorgehobene relevante Werte, beschreibende sekundäre Texte, Consolas für Hashes/Logs.
5. **Dark/Light:** Alle zentralen Brushes sind austauschbar. Bestehende gespeicherte Präferenz bleibt erhalten; die Vorgabe für neue Profile ist hell.
6. **Bewegung:** Keine automatischen Sprünge, Parallax-Animationen oder Bounce-Effekte. Direkte Maus-/Tastatur-Rückmeldung ohne erzwungenes Motion-Verhalten. GPU-Drag-and-Drop bleibt erhalten.
7. **Zugänglichkeit:** Kontrast, sichtbare Keyboard-Fokusumrandung und mehrere Rückmeldeformen (Text statt allein Farbe). Schriftgrößen und Fenster können durch Windows angepasst werden; weitere UI-Accessibility-Tests auf realen Geräten bleiben erforderlich.
8. **Verantwortung:** Fehlgeschlagene private GitHub-API-Abfragen blockieren nicht mehr die Anzeige anderer Bereiche. Kostenschätzungen bleiben ausdrücklich als Schätzungen gekennzeichnet; Token-Eingaben sind maskiert.

## Technische Umsetzung

- `App.xaml`: zentrale, dynamische Farbressourcen sowie Styles für Navigation, Buttons, Text-/Passwortfelder, Tabellen und Karten.
- `MainWindow.xaml`: feste Navigation, Top-Bar, Seitencontainer und Status-Footer; bestehende ViewModel-Kommandos erhalten.
- `MainWindow.xaml.cs`: Farbpalette und UI-Smoke-Test, der alle sieben Seiten nacheinander rendert.
- `MainViewModel.cs`: unabhängige Initialisierung von Git/Thunder und gesonderter Git-Sync-Status.
- GitHub Actions: self-contained Windows-Publish und `--ui-smoke-test`; testet das tatsächliche Fenster, aber ersetzt **keine** manuelle Prüfung von Schrift-Skalierung, Tastaturbedienung, Resize und Fensterdarstellung am Endgerät.

Bewusst keine Kopie von Web-CSS, JavaScript Pointer Events oder Framer-Motion-Springs in WPF; diese APIs gelten nicht für das native Zielsystem.

## Optimierung der Seite „Neue Instanz“ (Alpha 0.1.0-alpha.8)

Ausgangsbefund anhand eines echten Windows-Screenshots: Die GPU-Zeilen verwendeten horizontale StackPanels mit sehr kleinen, teilweise schwer lesbaren Eingaben und ungünstigen Abständen. Die Ressourcenkonfiguration erschien als dichtes WrapPanel, die Startvorschau lag weit unterhalb der sichtbaren Fläche und die Hauptaktion musste durch Scrollen gesucht werden. Die explizite TextBlock-Vordergrundfarbe überlagerte außerdem die von primären Buttons geerbte Schriftfarbe.

Umsetzung:
- GPU-Liste mit breiten, gleichmäßigen Zeilen als eigenständige Auswahlkarten. Einheitliches vierteiliges Raster für Aktivierung, GPU-Modell, vCPU-Wert und Stundenpreis. Mouse-Drag-and-Drop und Pfeiltasten-/Schaltflächen bleiben erhalten.
- Ressourcenkonfiguration als zweispaltiges Raster mit vollständigen, einheitlich formatierten Beschriftungen. Die Feldgrößen passen sich der Breite der Seite an.
- Startvorschau mit separater Überschrift, Hinweis auf fehlende Live-Preise und lesbarem Monospace-Textfeld.
- Fixierter Aktionsbereich am unteren Rand der Instanzseite. Der Button „Instanz erstellen …“ führt denselben, weiterhin zwingend bestätigten kostenpflichtigen Create-Command aus.
- TextBlock erbt die Vordergrundfarbe von umgebenden Controls, damit der primäre Button auch im hellen und dunklen Modus den vorgesehenen Schriftkontrast hat.
- Schriftgrößen der Standardbedienelemente angehoben und GPU-Zeilen mit Fokus-, Hover- und Auswahlindikatoren ausgestattet.

**Testgrenzen:** Der automatische WPF-GUI-Test rendert alle sieben Seiten, prüft die Kopierbarkeit des Logs und kontrolliert auf der Instanzseite GPU-Liste und fixierten Aktionsbutton bei Standardbreite und 1080px-Minimalbreite. Er ersetzt nicht die manuelle Sichtprüfung bei 125%/150% DPI, Bildschirmvergrößerung und kleinen Displays. Externe Abläufe und kostenpflichtige Operationen werden im GUI-Test nicht aufgerufen.

## Geführte Bereitstellung und Pod-Verwaltung (Alpha 0.1.0-alpha.9)

Ziel: Der bisher tabbasierte Ablauf wurde mit expliziten nächsten Schritten verbunden. Aus der Übersicht lädt ein klarer Button den privaten Katalog und öffnet die Workflow-Auswahl. Dort ist die Mehrfachauswahl eindeutig markiert; die Folgeaktion öffnet erst nach Konfliktprüfung die Hardwarekonfiguration. Die Hardwareseite zeigt die hergeleiteten README-Referenzen **vor** den editierbaren GPU-/CPU-/Disk-Feldern. Empfehlungen werden erst auf ausdrücklichen Klick übernommen, nicht unbemerkt. Die permanente Create-Leiste bleibt und nach erfolgreicher Erstellung öffnet sich die Pod-Verwaltung mit Status, SSH-/ComfyUI- und Snapshot-Aktionen. „Installation & Logs“ führt in die bestehenden Setup- und Protokollfunktionen.

Alle Daten bleiben im lokalen UI bzw. im privaten GitHub-API-Abruf; keine zusätzliche öffentliche Workflow-Quelle. Verfügbarkeit und Preise sind nicht live verifiziert. Navigation und reine Empfehlungen erzeugen keine GPU-Instanz und lösen keine Downloads aus.

## Systemstatus und Einrichtung (Alpha 0.1.0-alpha.11)

Als achte Seite ist **Systemstatus** neben den bestehenden Arbeitsbereichen permanent über die Seitenleiste erreichbar. Ein Schnellzugriff auf der Übersicht führt direkt dorthin. Die Oberfläche verwendet dieselben Karten, lesbare Statusbeschriftungen (nicht Farbe allein), deutliche Zeitstempel und passende Erfolg-/Warn-/Fehlerfarben in Hell und Dunkel.

Jede der sieben Prüfungen hat einen klaren Titel, einen erklärenden Status, ein kurzes Einrichtungsverfahren, einen offiziellen Link zum jeweiligen Konto-/Dokumentationsbereich sowie die Navigation zum passenden GUI-Tab. Die Übersicht trennt erfolgreich **getestete** Verbindungen von lediglich **vorhandenen** Dateien und nicht prüfbaren Bedingungen; optional fehlende Provider-Tokens sind nicht mit zwingenden Voraussetzungen gleichgesetzt.

Keine automatischen Ressourcenoperationen, kein automatisches SSH-Hostvertrauen. Die GUI-Smoke-Prüfung deckt alle acht Ansichten inklusive Systemstatus ohne Netzwerkzugriff ab.
