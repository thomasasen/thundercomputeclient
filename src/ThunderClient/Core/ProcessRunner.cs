using System.Diagnostics;
using System.ComponentModel;
using System.Text;

namespace ThunderClient.Core;

public sealed record CommandResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;
    public string Combined => Stdout + "\n" + Stderr;
}

public sealed class ExternalCommandException(string operation, CommandResult result)
    : Exception($"{operation} fehlgeschlagen (Exitcode {result.ExitCode}). {result.Stderr.Trim()}")
{
    public CommandResult Result { get; } = result;
}

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
        TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessRunner : ICommandRunner
{
    // The desktop process may retain an older PATH than a newly opened PowerShell.
    // Thunder's Windows installer also installs under Program Files (x86)/tnr.
    // Use an explicit absolute exe path when available; do not require an elevated shell.
    public static string ResolveThunderCliPath(string? searchPath = null,
        string? programFilesX86 = null, string? programFiles = null)
    {
        var currentPath = searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var entry in currentPath.Split(Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = entry.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(directory)) continue;
            var exe = Path.Combine(directory, "tnr.exe");
            if (File.Exists(exe)) return exe;
        }
        var standardLocations = new[]
        {
            programFilesX86 ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            programFiles ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        foreach (var root in standardLocations)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) continue;
            var exe = Path.Combine(root, "tnr", "tnr.exe");
            if (File.Exists(exe)) return exe;
        }
        // Let Process.Start report the actual Windows error if no path is found.
        return "tnr";
    }

    public async Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
        TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
        CancellationToken cancellationToken = default)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timer.CancelAfter(timeout);
        var resolved = executable.Equals("tnr", StringComparison.OrdinalIgnoreCase)
            ? ResolveThunderCliPath() : executable;
        var start = new ProcessStartInfo(resolved)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            CreateNoWindow = true
        };
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Prozessstart fehlgeschlagen: {executable}");
        }
        catch (Win32Exception ex)
        {
            var reason = ex.NativeErrorCode switch
            {
                2 or 3 => "Das Programm oder ein zugehöriger Pfad wurde vom GUI-Prozess nicht gefunden.",
                5 => "Windows hat den Programmstart verweigert (Zugriffsrechte oder Sicherheitsrichtlinie).",
                193 => "Die Datei ist keine ausführbare Anwendung für diese Windows-Architektur.",
                740 => "Die Anwendung fordert erhöhte Rechte an. Der Client führt keine automatische Rechteerhöhung aus.",
                _ => ex.Message
            };
            var action = executable.Equals("tnr", StringComparison.OrdinalIgnoreCase)
                ? " In PowerShell 'Get-Command tnr' und 'tnr status --json --no-wait' prüfen. " +
                  "Nach einer Installation die Desktop-App neu starten."
                : "";
            throw new InvalidOperationException(
                $"Prozess '{executable}' konnte nicht gestartet werden " +
                $"(Windows-Fehler {ex.NativeErrorCode}, Pfad '{resolved}'): {reason}.{action}", ex);
        }

        try
        {
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            async Task ReadLinesAsync(StreamReader reader, StringBuilder buffer)
            {
                while (true)
                {
                    var line = await reader.ReadLineAsync(timer.Token);
                    if (line is null) break;
                    if (buffer.Length > 4_000_000) throw new InvalidDataException("Prozessausgabe zu groß");
                    buffer.AppendLine(line);
                    onLine?.Invoke(line);
                }
            }

            var outTask = ReadLinesAsync(process.StandardOutput, stdout);
            var errTask = ReadLinesAsync(process.StandardError, stderr);
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), timer.Token);
                await process.StandardInput.FlushAsync(timer.Token);
                process.StandardInput.Close();
            }

            await Task.WhenAll(outTask, errTask, process.WaitForExitAsync(timer.Token));
            return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}

public static class CommandExtensions
{
    public static async Task<CommandResult> RequireSuccess(this ICommandRunner runner,
        string executable, IEnumerable<string> args, TimeSpan timeout,
        string? input = null, Action<string>? onLine = null, CancellationToken ct = default)
    {
        var result = await runner.RunAsync(executable, args, timeout, input, onLine, ct);
        if (!result.Success) throw new ExternalCommandException(executable, result);
        return result;
    }
}

