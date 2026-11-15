using System.Text;
using System.Text.RegularExpressions;

namespace ThunderClient;

internal static class StartupDiagnostics
{
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ThunderClient", "logs", "startup.log");

    public static void Trace(string message) => Write("INFO: " + message);

    public static void Record(string stage, Exception exception)
    {
        var details = new StringBuilder();
        details.AppendLine(stage);
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            details.AppendLine(current.GetType().FullName + ": " + Redact(current.Message));
            if (current.StackTrace is { } trace) details.AppendLine(Redact(trace));
        }
        Write(details.ToString());
    }

    private static string Redact(string text)
    {
        text = Regex.Replace(text,
            @"(?i)\b(?:github_pat_|ghp_|gho_|ghu_|ghs_|ghr_|hf_)[a-z0-9_\-]+",
            "[TOKEN ENTFERNT]");
        text = Regex.Replace(text,
            @"(?i)\b(bearer\s+)[^\s,;]+", "$1[TOKEN ENTFERNT]");
        return Regex.Replace(text,
            @"(?i)\b(token|api[_-]?key|password|secret)\s*[:=]\s*[^\s,;]+",
            "$1=[TOKEN ENTFERNT]");
    }

    private static void Write(string entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 256_000)
                File.Move(LogPath, LogPath + ".old", overwrite: true);
            File.AppendAllText(LogPath,
                DateTimeOffset.Now.ToString("O") + "  " + entry + Environment.NewLine);
        }
        catch (Exception)
        {
            // Diagnostik darf die GUI niemals am Start hindern.
        }
    }
}
