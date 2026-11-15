# Oberflächendesign – Thunder Compute Client

## Ziel

Die Anwendung ist ein Windows-Desktop-Arbeitsplatz für technisch anspruchsvolle, teilweise kostenpflichtige Thunder-/ComfyUI-Aufgaben. Sie muss zuverlässig, lesbar und vorhersehbar sein. Gestaltung darf keine angebliche Hardwareverfügbarkeit oder Abrechnungssicherheit suggerieren.

## Referenz und Anpassung

Referenz: [emilkowalski/skills – apple-design/SKILL.md](https://github.com/emilkowalski/skills/blob/main/skills/apple-design/SKILL.md), abgerufen am 15.11.2026. Die Quelle beschreibt weborientierte Interaktion und Apples Gestaltungsprinzipien; hier werden **Prinzipien** in native WPF-Steuerelemente übersetzt. Die Anwendung wird nicht als Apple-Anwendung oder macOS-Port ausgegeben.

1. **Orientierung:** Eine feste Seitenleiste mit sieben klar benannten Zielen ersetzt die schwer lesbare Tab-Leiste. Aktueller Bereich ist markiert; jeder Bereich hat einen eigenständigen Seitentitel.
2. **Sofortiges Feedback:** Hover, Pressed- und Keyboard-Focus-Zustände; eine dauerhafte Statuszeile und eine Git-Synchronisierungskarte. Kostenrelevante Aktionen behalten ihre explizite Bestätigung.
3. **Hierarchie:** Hintergrund, Sidebar, Inhaltsfläche und Eingabefeld sind über wenige konsistente Farbebenen getrennt. Karten mit 16px-Radius und großzügigem Abstand, ohne dekorative Überlagerungen.
4. **Typografie:** Native Segoe UI, große Seitenüberschriften, niedrigere Abschnittstitel, hervorgehobene relevante Werte, beschreibende sekundäre Texte, Consolas für Hashes/Logs.
5. **Dark/Light:** Alle zentralen Brushes sind austauschbar. Bestehende gespeicherte Präferenz bleibt erhalten; die Vorgabe für neue Profile ist hell.
6. **Bewegung:** Keine automatischen Sprünge, Parallax-Animationen oder Bounce-Effekte. Direkte Maus-/Tastatur-Rückmeldung ohne erzwungenes Motion-Verhalten. GPU-Drag-and-Drop bleibt erhalten.
7. **Zugänglichkeit:** Kontrast, sichtbare Keyboard-Fokusumrandung und mehrere Rückmeldeformen (Text statt allein Farbe). Schriftgrößen und Fenster können durch Windows angepasst werden; weitere UI-Accessibility-Tests auf realen Geräten bleiben erforderlich.
8. **Verantwortung:** Fehlgeschlagener Git-Checkout blockiert nicht mehr die Anzeige anderer Bereiche. Kostenschätzungen bleiben ausdrücklich als Schätzungen gekennzeichnet; Token-Eingaben sind maskiert.

## Technische Umsetzung

- `App.xaml`: zentrale, dynamische Farbressourcen sowie Styles für Navigation, Buttons, Text-/Passwortfelder, Tabellen und Karten.
- `MainWindow.xaml`: feste Navigation, Top-Bar, Seitencontainer und Status-Footer; bestehende ViewModel-Kommandos erhalten.
- `MainWindow.xaml.cs`: Farbpalette und UI-Smoke-Test, der alle sieben Seiten nacheinander rendert.
- `MainViewModel.cs`: unabhängige Initialisierung von Git/Thunder und gesonderter Git-Sync-Status.
- GitHub Actions: self-contained Windows-Publish und `--ui-smoke-test`; testet das tatsächliche Fenster, aber ersetzt **keine** manuelle Prüfung von Schrift-Skalierung, Tastaturbedienung, Resize und Fensterdarstellung am Endgerät.

Bewusst keine Kopie von Web-CSS, JavaScript Pointer Events oder Framer-Motion-Springs in WPF; diese APIs gelten nicht für das native Zielsystem.
