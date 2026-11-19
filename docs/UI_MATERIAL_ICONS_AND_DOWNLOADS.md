# Oberflächenbedienung: Material-Icons, Downloads-Tabelle und Statusfarben

## Icons

Die Anwendung verwendet lokale **Google Material Icons** aus dem offiziellen Material-Design-Icons-Katalog: https://fonts.google.com/icons bzw. https://github.com/google/material-design-icons. Die 24×24-Vektorpfade sind in `UI/MaterialIconConverter.cs` als unveränderliche Geometrien eingebunden; kein Icon-Webservice, keine Schriftdatei und kein Netzwerkzugriff beim Start. Das mit v0.1.6 eingeführte Blitz-Logo bleibt das App-/Installer-Icon.

- Eindeutige Bedeutungen: Aktualisieren = Kreispfeil, Starten = Play, Stoppen/Abbrechen = Quadrat, Löschen = Papierkorb, Speichern = Diskette, Kopieren = Kopiersymbol, Kosten = Diagramm, externe Seiten = Öffnen, SSH = Terminal, Instanzen = Server.
- Die Beschriftung der Buttons bleibt vollständig sichtbar, klickbar und für Barrierefreiheit/Automatisierung unverändert. Unbekannte Beschriftungen erhalten **kein** irreführendes Symbol.
- Icons folgen der aktuellen hellen/dunklen Theme-Vordergrundfarbe. Statussymbole verwenden zusätzlich die fachlich begründete Statusfarbe.

**Lizenz:** Google Material Icons, Copyright Google, Apache License 2.0. Vollständiger Lizenztext: `licenses/material-design-icons-APACHE-2.0.txt`. Die geometrischen Pfade wurden für WPF eingebettet und erhalten die ursprüngliche Form.

## Downloads & Logs

Die vorhandene Downloadtabelle bleibt scrollbar, lesbar und virtualisiert. Neu:

1. **Klick auf jede Spaltenüberschrift** sortiert aufsteigend, erneuter Klick absteigend. Die Vorbelegung ist unverändert; keine Sortieraktion löscht oder verändert Download-Logs.
2. **Modelldatei** sortiert nach Dateiname. **Status** sortiert nach dem unverändert vom Upstream übermittelten Statuswort. **Fortschritt** sortiert **numerisch nach tatsächlich gemeldeten Prozentwerten**, nicht lexikografisch. Wenn ein Wert nicht vorliegt oder ungültig ist, bleibt der numerische Sortierschlüssel leer. Nicht vorhandene Zahlen werden nicht geschätzt.
3. Status- und Fortschrittsänderungen lösen PropertyChanged aus; aktive Spaltensortierung bleibt bei nachkommenden Logereignissen aktuell (WPF LiveSorting).
4. Der waagerechte **Trennbalken zwischen Downloadtabelle und Protokoll** lässt sich mit der Maus nach oben/unten ziehen. Beide Bereiche behalten eigene Mindesthöhen und Scrollbalken. Spaltenbreiten lassen sich ebenfalls an den Headerkanten ändern.
5. Statusfarben/Icons (auch im Dunkelmodus): **GELADEN/VORHANDEN** grün mit Häkchen; **LAEUFT/FORTSCHRITT** blau mit Sync; **RETRY** gelb mit Warnsymbol; **FEHLER** rot mit Fehlersymbol; **WARTET** neutral mit Uhr. Systemchecks erhalten entsprechende Abzeichen (grün, gelb, rot, neutral); RUNNING-Pods werden grün, fehlgeschlagene Pods rot gekennzeichnet.

Die Farben sind keine zusätzlichen technischen Prüfungen. Sie visualisieren nur die von Thunder bzw. vom zentralen Download-Monitor tatsächlich gelieferten Zustände. Es wird keine neue Netzwerkabfrage und kein automatischer Pod-Neustart ausgelöst.

## Qualitätssicherung

Die Windows-CI prüft den nativen WPF-Start (alle acht Bereiche, Minimalbreite, Tabelle/Sortierschlüssel, Trenner), .NET-Tests, Publish und Installer. Regressionstests kontrollieren insbesondere numerische Prozentsortierung und Benachrichtigung bei Live-Updates. Die Linux-Integrationstests bleiben unverändert. Eine manuelle Prüfung der Mausbedienung, Kontraste, Bildschirmleser und DPI-Skalierung auf Windows ist ergänzend sinnvoll.
