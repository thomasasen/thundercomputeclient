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
    public async Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
        TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
        CancellationToken cancellationToken = default)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timer.CancelAfter(timeout);
        var start = new ProcessStartInfo(executable)
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
        catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3)
        {
            var description = executable.ToLowerInvariant() switch
            {
                "tnr" => "Die offizielle Thunder-CLI 'tnr' wurde nicht gefunden. " +
                    "Bitte installieren, im Terminal mit 'Get-Command tnr' prüfen " +
                    "und den Client nach einer PATH-Änderung neu starten.",
                "git" => "Git for Windows wurde nicht gefunden. " +
                    "Bitte installieren und im Terminal mit 'Get-Command git' prüfen.",
                "ssh" or "ssh-keygen" or "ssh-keyscan" =>
                    "OpenSSH fehlt im Windows-PATH. Bitte den Windows-OpenSSH-Client installieren.",
                _ => $"Programm '{executable}' wurde nicht gefunden."
            };
            throw new InvalidOperationException(description, ex);
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

