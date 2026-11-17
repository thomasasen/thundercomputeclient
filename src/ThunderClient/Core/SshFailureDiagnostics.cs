namespace ThunderClient.Core;

/// <summary>
/// Distinguishes the user's explicit non-approval from technical SSH host checks.
/// This classification never approves a key or changes known_hosts.
/// The caller must redact credentials before passing a message.
/// </summary>
public enum SshFailureKind
{
    Other,
    FingerprintNotConfirmed,
    TechnicalHostCheckFailed
}

public static class SshFailureDiagnostics
{
    public static SshFailureKind Classify(Exception exception, string redactedMessage)
    {
        if (exception is SshFingerprintNotConfirmedException)
            return SshFailureKind.FingerprintNotConfirmed;

        if (redactedMessage.Contains("SSH-Hostschlüssel", StringComparison.OrdinalIgnoreCase) ||
            redactedMessage.Contains("ssh-keyscan", StringComparison.OrdinalIgnoreCase) ||
            redactedMessage.Contains("Programm 'ssh'", StringComparison.OrdinalIgnoreCase) ||
            redactedMessage.Contains("Programm 'ssh-keygen'", StringComparison.OrdinalIgnoreCase))
            return SshFailureKind.TechnicalHostCheckFailed;

        return SshFailureKind.Other;
    }

    public static string SummarizeRedactedCause(string redactedMessage)
    {
        const int maximumLength = 1200;
        return redactedMessage.Length <= maximumLength
            ? redactedMessage
            : redactedMessage[..maximumLength] + " … (gekürzt)";
    }
}
