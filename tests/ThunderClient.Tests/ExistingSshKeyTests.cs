using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class ExistingSshKeyTests
{
    private static InstanceItem ExistingPod() =>
        new("0", "uuid-existing-pod", "existing", "RUNNING",
            "l40", "203.0.113.10", 40002, null);

    [Fact]
    public void ExplicitAttachedPrivateKeyNeedsNoStateChangingThunderCall()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "thunder-attached-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "TEST KEY MATERIAL ONLY");
        try
        {
            var ssh = ThunderCli.TryExistingSshKey(ExistingPod(), path, null);
            Assert.NotNull(ssh);
            Assert.Equal("0", ssh.InstanceId);
            Assert.Equal(40002, ssh.Port);
            Assert.Equal(path, ssh.KeyFile);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExplicitMissingOrPublicKeyFailsClosed()
    {
        var missing = Path.Combine(Path.GetTempPath(),
            "missing-thunder-key-" + Guid.NewGuid().ToString("N"));
        var error = Assert.Throws<InvalidOperationException>(() =>
            ThunderCli.TryExistingSshKey(ExistingPod(), missing, null));
        Assert.Contains("Keine Thunder-Key-Registrierung", error.Message);
        var pub = missing + ".pub";
        File.WriteAllText(pub, "TEST PUB KEY");
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                ThunderCli.TryExistingSshKey(ExistingPod(), pub, null));
        }
        finally { File.Delete(pub); }
    }

    [Fact]
    public void LocallyPersistedTnrKeyIsDiscoveredReadOnly()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "tnr-home-test-" + Guid.NewGuid().ToString("N"));
        var keys = Path.Combine(folder, "keys");
        Directory.CreateDirectory(keys);
        var path = Path.Combine(keys, ExistingPod().Uuid);
        File.WriteAllText(path, "TEST PRIVATE KEY");
        try
        {
            var ssh = ThunderCli.TryExistingSshKey(ExistingPod(), null, folder);
            Assert.NotNull(ssh);
            Assert.Equal(path, ssh.KeyFile);
            File.Delete(path);
            Assert.Null(ThunderCli.TryExistingSshKey(ExistingPod(), null, folder));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void NeverAcceptsInvalidProviderHostOrNonRunningPod()
    {
        var pod = ExistingPod();
        Assert.Throws<InvalidOperationException>(() =>
            ThunderCli.TryExistingSshKey(pod with { Port = 0 }, null, null));
        Assert.Throws<InvalidOperationException>(() =>
            ThunderCli.TryExistingSshKey(pod with { Ip = "example.org" }, null, null));
        Assert.Throws<InvalidOperationException>(() =>
            ThunderCli.TryExistingSshKey(pod with { Status = "STARTING" }, null, null));
    }
}
