using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderClient.Core;

// Verarbeitet ausschließlich tatsächlich ausgegebene Ereignisse des externen download_monitor.py.
public sealed record DownloadEvent(string Model, string State, string Progress);

public sealed class DownloadItem : Bindable
{
    private string _state;
    private string _progress;
    public DownloadItem(string model, string state, string progress)
    { Model = model; _state = state; _progress = progress; }
    public string Model { get; }
    public string State { get => _state; set => Set(ref _state, value); }
    public string Progress
    {
        get => _progress;
        set
        {
            if (Set(ref _progress, value))
                OnPropertyChanged(nameof(ProgressSortValue));
        }
    }

    // Numeric table sorting, never an inferred transfer percentage.
    // Unknown/incomplete values remain null and sort separately from real %.
    public decimal? ProgressSortValue
    {
        get
        {
            var match = Regex.Match(Progress, @"(?<!\d)(?<percent>\d{1,3}(?:[.,]\d+)?)\s*%",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!match.Success) return null;
            var raw = match.Groups["percent"].Value.Replace(',', '.');
            return decimal.TryParse(raw, System.Globalization.NumberStyles.AllowDecimalPoint,
                       System.Globalization.CultureInfo.InvariantCulture, out var value) &&
                   value is >= 0 and <= 100 ? value : null;
        }
    }
}

public static class DownloadTracker
{
    private static readonly Regex EventLine = new(
        "\\[DOWNLOAD-STATUS\\]\\s+(?<state>WARTET|LAEUFT|FORTSCHRITT|RETRY|GELADEN|VORHANDEN|FEHLER)\\s+(?<model>\\\"(?:\\\\.|[^\\\"\\\\])*\\\")(?<tail>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Percent = new(
        @"\((?<percent>\d{1,3})%\)", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static bool TryParse(string line, out DownloadEvent? parsed)
    {
        parsed = null;
        var match = EventLine.Match(line);
        if (!match.Success) return false;
        string? model;
        try { model = JsonSerializer.Deserialize<string>(match.Groups["model"].Value); }
        catch (JsonException) { return false; }
        if (string.IsNullOrWhiteSpace(model)) return false;

        var state = match.Groups["state"].Value;
        var tail = match.Groups["tail"].Value.Trim();
        var progress = "Keine belastbaren Transferdaten";
        if (state == "FORTSCHRITT")
        {
            // Transferdaten stammen direkt aus der Download-Monitor-Zeile.
            progress = tail.TrimStart(':', ' ');
            var pct = Percent.Match(tail);
            if (pct.Success && int.TryParse(pct.Groups["percent"].Value, out var value) && value > 100)
                progress = "Ungültige Prozentangabe ignoriert";
        }
        parsed = new DownloadEvent(model, state, progress);
        return true;
    }
}

