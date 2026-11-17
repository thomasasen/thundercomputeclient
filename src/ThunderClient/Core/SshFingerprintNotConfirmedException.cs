namespace ThunderClient.Core;

/// <summary>
/// The user did not approve the independently verified SSH host fingerprint.
/// This is not an OpenSSH timeout, a networking error or an authorization failure.
/// An unapproved host key must never be persisted or used for authentication.
/// </summary>
public sealed class SshFingerprintNotConfirmedException : OperationCanceledException
{
    public SshFingerprintNotConfirmedException()
        : base("SSH-Fingerprint wurde nicht bestätigt. Es wurde kein SSH-Hostschlüssel gespeichert.")
    {
    }
}
