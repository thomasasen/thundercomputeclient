using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class SshFailureDiagnosticsTests
{
    [Fact]
    public void FingerprintDecisionIsNotMisclassifiedAsCommandTimeout()
    {
        var exception = new SshFingerprintNotConfirmedException();
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(SshFailureKind.FingerprintNotConfirmed,
            SshFailureDiagnostics.Classify(exception, exception.Message));
        Assert.Contains("nicht bestätigt", exception.Message);
    }

    [Theory]
    [InlineData("SSH-Hostschlüssel konnte nicht abgefragt werden")]
    [InlineData("Windows OpenSSH ssh-keyscan kann den Schlüsselaustausch nicht verarbeiten")]
    [InlineData("Programm 'ssh' hat innerhalb von 18 Sekunden nicht geantwortet")]
    [InlineData("Programm 'ssh-keygen' hat innerhalb von 10 Sekunden nicht geantwortet")]
    public void TechnicalFailuresAreNotReportedAsFingerprintRejection(string reason)
    {
        var error = new TimeoutException(reason);
        Assert.Equal(SshFailureKind.TechnicalHostCheckFailed,
            SshFailureDiagnostics.Classify(error, reason));
        Assert.Equal(reason, SshFailureDiagnostics.SummarizeRedactedCause(reason));
    }

    [Fact]
    public void UnrelatedErrorsAreNotPresentedAsHostKeyFailures()
    {
        var error = new InvalidOperationException("Civitai token not available");
        Assert.Equal(SshFailureKind.Other,
            SshFailureDiagnostics.Classify(error, error.Message));
    }

    [Fact]
    public void TechnicalDiagnosticIsCappedForMessageBox()
    {
        var error = new string('x', 1500);
        var displayed = SshFailureDiagnostics.SummarizeRedactedCause(error);
        Assert.StartsWith(new string('x', 1200), displayed);
        Assert.EndsWith("(gekürzt)", displayed);
        Assert.True(displayed.Length < error.Length);
    }
}
