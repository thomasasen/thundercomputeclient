using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ThunderClient.UI;

/// <summary>
/// Material Icons (Google), filled 24px SVG path data (Apache-2.0).
/// https://github.com/google/material-design-icons
/// Local frozen vector geometries: no online font, no WebView, no runtime download.
/// Keep button Content as text for screen readers, keyboard navigation and smoke tests.
/// </summary>
public sealed class MaterialIconConverter : IValueConverter
{
    private static Geometry G(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }

    private static readonly IReadOnlyDictionary<string, Geometry> Paths =
        new Dictionary<string, Geometry>(StringComparer.Ordinal)
        {
            ["dashboard"] = G("M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z"),
            ["list"] = G("M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z"),
            ["dns"] = G("M20 13H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1v-6c0-.55-.45-1-1-1zM7 19c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zM20 3H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1V4c0-.55-.45-1-1-1zM7 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z"),
            ["settings"] = G("M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.07-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61 l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41 h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.74,8.87 C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.07,0.94l-2.03,1.58 c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54 c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.44-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96 c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.47-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6 s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z"),
            ["shield"] = G("M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm-2 16l-4-4 1.41-1.41L10 14.17l6.59-6.59L18 9l-8 8z"),
            ["refresh"] = G("M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"),
            ["check_circle"] = G("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"),
            ["error"] = G("M11 15h2v2h-2zm0-8h2v6h-2zm.99-5C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8z"),
            ["warning"] = G("M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z"),
            ["schedule"] = G("M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8z M12.5 7H11v6l5.25 3.15.75-1.23-4.5-2.67z"),
            ["play"] = G("M8 5v14l11-7z"),
            ["stop"] = G("M6 6h12v12H6z"),
            ["save"] = G("M17 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"),
            ["copy"] = G("M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z"),
            ["download"] = G("M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"),
            ["delete"] = G("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"),
            ["external"] = G("M19 19H5V5h7V3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7zM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7z"),
            ["backup"] = G("M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z"),
            ["restore"] = G("M13 3c-4.97 0-9 4.03-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42C8.27 19.99 10.51 21 13 21c4.97 0 9-4.03 9-9s-4.03-9-9-9zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"),
            ["up"] = G("M4 12l1.41 1.41L11 7.83V20h2V7.83l5.58 5.59L20 12l-8-8-8 8z"),
            ["down"] = G("M20 12l-1.41-1.41L13 16.17V4h-2v12.17l-5.58-5.59L4 12l8 8 8-8z"),
            ["search"] = G("M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"),
            ["build"] = G("M22.7 19l-9.1-9.1c.9-2.3.4-5-1.5-6.9-2-2-5-2.4-7.4-1.3L9 6 6 9 1.6 4.7C.4 7.1.9 10.1 2.9 12.1c1.9 1.9 4.6 2.4 6.9 1.5l9.1 9.1c.4.4 1 .4 1.4 0l2.3-2.3c.5-.4.5-1.1.1-1.4z"),
            ["assessment"] = G("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zM9 17H7v-7h2v7zm4 0h-2V7h2v10zm4 0h-2v-4h2v4z"),
            ["credit_card"] = G("M20 4H4c-1.11 0-1.99.89-1.99 2L2 18c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z"),
            ["key"] = G("M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z"),
            ["info"] = G("M11,7h2v2h-2V7z M11,11h2v6h-2V11z M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10s10-4.48,10-10S17.52,2,12,2z M12,20 c-4.41,0-8-3.59-8-8s3.59-8,8-8s8,3.59,8,8S16.41,20,12,20z"),
            ["cloud_download"] = G("M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM17 13l-5 5-5-5h3V9h4v4h3z"),
            ["terminal"] = G("M20,4H4C2.89,4,2,4.9,2,6v12c0,1.1,0.89,2,2,2h16c1.1,0,2-0.9,2-2V6C22,4.9,21.11,4,20,4z M20,18H4V8h16V18z M18,17h-6v-2 h6V17z M7.5,17l-1.41-1.41L8.67,13l-2.59-2.59L7.5,9l4,4L7.5,17z"),
            ["sync"] = G("M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"),
            ["folder"] = G("M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V8h16v10z"),
            ["lock"] = G("M18 8h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zm-6 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zm3.1-9H8.9V6c0-1.71 1.39-3.1 3.1-3.1 1.71 0 3.1 1.39 3.1 3.1v2z"),
            ["storage"] = G("M2 20h20v-4H2v4zm2-3h2v2H4v-2zM2 4v4h20V4H2zm4 3H4V5h2v2zm-4 7h20v-4H2v4zm2-3h2v2H4v-2z"),
            ["update"] = G("M21,10.12h-6.78l2.74-2.82c-2.73-2.7-7.15-2.8-9.88-0.1c-2.73,2.71-2.73,7.08,0,9.79s7.15,2.71,9.88,0 C18.32,15.65,19,14.08,19,12.1h2c0,1.98-0.88,4.55-2.64,6.29c-3.51,3.48-9.21,3.48-12.72,0c-3.5-3.47-3.53-9.11-0.02-12.58 s9.14-3.47,12.65,0L21,3V10.12z M12.5,8v4.25l3.5,2.08l-0.72,1.21L11,13V8H12.5z"),
        };

    public static string? IconForLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var label = raw.Trim();
        // State names are intentionally not translated: they come from the
        // existing remote monitor and read-only readiness checks.
        if (label is "GELADEN" or "VORHANDEN" or "Erfolgreich getestet" or
            "Vorhanden" or "RUNNING") return "check_circle";
        if (label is "FEHLER" or "Fehlt" or "Fehler" or "FAILED" or "ERROR") return "error";
        if (label is "RETRY" or "Einrichtung nötig") return "warning";
        if (label is "WARTET" or "Nicht geprüft" or "QUEUED" or "PENDING") return "schedule";
        if (label is "LAEUFT" or "FORTSCHRITT" or "Prüfe …" or "STARTING" or
            "PROVISIONING") return "sync";
        if (label is "STOPPED" or "TERMINATED") return "stop";

        if (label.Contains("löschen", StringComparison.OrdinalIgnoreCase)) return "delete";
        if (label.Contains("abbrechen", StringComparison.OrdinalIgnoreCase)) return "stop";
        if (label.Contains("kopieren", StringComparison.OrdinalIgnoreCase)) return "copy";
        if (label.Contains("speichern", StringComparison.OrdinalIgnoreCase)) return "save";
        if (label.Contains("exportieren", StringComparison.OrdinalIgnoreCase)) return "download";
        if (label.Contains("Snapshot", StringComparison.OrdinalIgnoreCase)) return
            label.Contains("wiederherstellen", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Template", StringComparison.OrdinalIgnoreCase) ? "restore" : "backup";
        if (label.Contains("wiederherstellen", StringComparison.OrdinalIgnoreCase)) return "restore";
        if (label.Contains("neu starten", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("aktualisieren", StringComparison.OrdinalIgnoreCase)) return "refresh";
        if (label.Contains("Thunder-Abrechnung", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Balance", StringComparison.OrdinalIgnoreCase)) return "credit_card";
        if (label.Contains("Tarif", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Kosten", StringComparison.OrdinalIgnoreCase)) return "assessment";
        if (label.Contains("README", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("öffnen", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("herunterladen", StringComparison.OrdinalIgnoreCase)) return "external";
        if (label.Contains("SSH", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Anmeldung", StringComparison.OrdinalIgnoreCase)) return "terminal";
        if (label.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Zugangsdaten", StringComparison.OrdinalIgnoreCase)) return "key";
        if (label.Contains("Systemstatus", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("sicher prüfen", StringComparison.OrdinalIgnoreCase)) return "shield";
        if (label.Contains("Instanz", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Pod", StringComparison.OrdinalIgnoreCase)) return "dns";
        if (label.Contains("ComfyUI", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Bootstrap", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("bereitstellen", StringComparison.OrdinalIgnoreCase)) return "play";
        if (label.Contains("Hardware", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Planungswerte", StringComparison.OrdinalIgnoreCase)) return "build";
        if (label.Contains("Modell", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Downloads", StringComparison.OrdinalIgnoreCase)) return "cloud_download";
        if (label.Contains("Workflow", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Katalog", StringComparison.OrdinalIgnoreCase)) return "list";
        if (label.Contains("Release", StringComparison.OrdinalIgnoreCase)) return "update";
        if (label.Contains("Nach oben", StringComparison.OrdinalIgnoreCase)) return "up";
        if (label.Contains("Nach unten", StringComparison.OrdinalIgnoreCase)) return "down";
        if (label.Contains("Hilfe", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Anleitung", StringComparison.OrdinalIgnoreCase)) return "info";
        if (label.Contains("Einstellungen", StringComparison.OrdinalIgnoreCase)) return "settings";
        if (label.Contains("Log", StringComparison.OrdinalIgnoreCase)) return "list";
        return null; // Unknown action: no decorative or misleading symbol.
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = IconForLabel(value as string);
        return key is not null && Paths.TryGetValue(key, out var geometry) ? geometry : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
