using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class SshHostKeyTests
{
    private const string Host = "203.0.113.17";
    private const int Port = 22022;
    private static SshConnection Endpoint() => new("test-id", Host, Port, "ignored-test-key");

    private static string TestEd25519Base64()
    {
        var key = new byte[51];
        BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(0, 4), 11);
        Encoding.ASCII.GetBytes("ssh-ed25519").CopyTo(key, 4);
        BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(15, 4), 32);
        for (var index = 19; index < key.Length; index++) key[index] = (byte)(index + 1);
        return Convert.ToBase64String(key);
    }

    private sealed class FakeRunner(params CommandResult[] results) : ICommandRunner
    {
        public int Calls { get; private set; }
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("ssh-keyscan", executable);
            Assert.Equal(["-T", "8", "-p", Port.ToString(), "-t", "ed25519", Host],
                arguments.ToArray());
            var index = Calls++;
            return Task.FromResult(results[Math.Min(index, results.Length - 1)]);
        }
    }

    [Fact]
    public void ValidatesExpectedNonstandardPortAndEd25519Fingerprint()
    {
        var key = TestEd25519Base64();
        var line = $"[{Host}]:{Port} ssh-ed25519 {key} ignored-comment";
        var result = SshService.ParseEd25519HostKey(Endpoint(), line);
        Assert.Equal($"[{Host}]:{Port} ssh-ed25519 {key}", result.KeyLine);
        var expected = "SHA256:" + Convert.ToBase64String(
            SHA256.HashData(Convert.FromBase64String(key))).TrimEnd('=');
        Assert.Equal(expected, result.Fingerprint);
    }

    [Fact]
    public async Task TransientRefusalIsRetriedBeforeShowingFingerprint()
    {
        var success = $"# SSH-2.0\n[{Host}]:{Port} ssh-ed25519 {TestEd25519Base64()}\n";
        var runner = new FakeRunner(
            new CommandResult(1, "", "Connection refused"),
            new CommandResult(0, success, ""));
        var service = new SshService(runner);
        var result = await service.ScanHostKeyAsync(Endpoint());
        Assert.StartsWith("SHA256:", result.Fingerprint);
        Assert.Equal(2, runner.Calls);
    }

    [Fact]
    public async Task ExhaustedRetriesKeepPortExitCodeAndErrorDetails()
    {
        var runner = new FakeRunner(new CommandResult(1, "", "Connection timed out"));
        var service = new SshService(runner);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ScanHostKeyAsync(Endpoint()));
        Assert.Equal(3, runner.Calls);
        Assert.Contains("Port " + Port, error.Message);
        Assert.Contains("Exitcode 1", error.Message);
        Assert.Contains("Connection timed out", error.Message);
        Assert.Contains("keinen unbekannten Fingerprint", error.Message);
    }

    [Fact]
    public void RejectsKeyForWrongHostEvenWhenServerSuppliesPlausibleKey()
    {
        var key = TestEd25519Base64();
        Assert.Throws<InvalidDataException>(() => SshService.ParseEd25519HostKey(
            Endpoint(), $"[203.0.113.99]:{Port} ssh-ed25519 {key}"));
    }

    [Fact]
    public void RejectsMalformedKeyMaterial()
    {
        Assert.Throws<InvalidDataException>(() => SshService.ParseEd25519HostKey(
            Endpoint(), $"[{Host}]:{Port} ssh-ed25519 !!NOT-BASE64!!"));
    }

    [Fact]
    public void RejectsBase64BlobThatIsNotActuallyEd25519()
    {
        var arbitraryBlob = Convert.ToBase64String(new byte[51]);
        Assert.Throws<InvalidDataException>(() => SshService.ParseEd25519HostKey(
            Endpoint(), $"[{Host}]:{Port} ssh-ed25519 {arbitraryBlob}"));
    }

    [Fact]
    public void RecognizesActualWin32OpenSshSntrupMismatch()
    {
        Assert.True(SshService.IsBrokenWindowsKeyscanKex(
            "# 216.81.200.228:40003 SSH-2.0-OpenSSH_8.9p1 Ubuntu-3ubuntu0.15 " +
            "choose_kex: unsupported KEX method sntrup761x25519-sha512@openssh.com"));
        Assert.False(SshService.IsBrokenWindowsKeyscanKex(
            "Connection refused"));
        Assert.False(SshService.IsBrokenWindowsKeyscanKex(
            "choose_kex: unsupported KEX method unknown-algorithm"));
    }

    private sealed class KexFailureThenSshRunner(bool produceValidKey) : ICommandRunner
    {
        public int Calls { get; private set; }
        public string? TemporaryFile { get; private set; }
        public bool UnsafeOptionsObserved { get; private set; }

        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            TimeSpan timeout, string? standardInput = null, Action<string>? onLine = null,
            CancellationToken cancellationToken = default)
        {
            var args = arguments.ToArray();
            Calls++;
            if (Calls == 1)
            {
                Assert.Equal("ssh-keyscan", executable);
                return Task.FromResult(new CommandResult(1, "",
                    "# 203.0.113.17:22022 SSH-2.0-OpenSSH_8.9p1 " +
                    "choose_kex: unsupported KEX method sntrup761x25519-sha512@openssh.com"));
            }
            Assert.Equal(2, Calls);
            Assert.Equal("ssh", executable);
            Assert.Contains("-N", args);
            Assert.Contains("none", args);
            Assert.Contains("HostKeyAlgorithms=ssh-ed25519", args);
            Assert.Contains("StrictHostKeyChecking=accept-new", args);
            Assert.Contains("PreferredAuthentications=none", args);
            Assert.Contains("PubkeyAuthentication=no", args);
            Assert.Contains("PasswordAuthentication=no", args);
            Assert.Contains("GlobalKnownHostsFile=NUL", args);
            Assert.DoesNotContain("-i", args);
            Assert.DoesNotContain("StrictHostKeyChecking=no", args);
            Assert.Null(standardInput);
            Assert.Equal("ubuntu@" + Host, args[^1]);
            var arg = Assert.Single(args, a =>
                a.StartsWith("UserKnownHostsFile=", StringComparison.Ordinal));
            TemporaryFile = arg["UserKnownHostsFile=".Length..];
            UnsafeOptionsObserved = args.Contains("StrictHostKeyChecking=no") ||
                args.Contains("-i");
            if (produceValidKey)
                File.WriteAllText(TemporaryFile,
                    $"[{Host}]:{Port} ssh-ed25519 {TestEd25519Base64()}\n");
            // SSH exits nonzero after anonymous handshake because authentication
            // is intentionally disabled. The collected public host key is still valid.
            return Task.FromResult(new CommandResult(255, "",
                "Permission denied (publickey)."));
        }
    }

    [Fact]
    public async Task WindowsKexBugFallsBackToTemporarySshWithoutTrustingOrAuthenticating()
    {
        var runner = new KexFailureThenSshRunner(produceValidKey: true);
        var service = new SshService(runner);
        var result = await service.ScanHostKeyAsync(Endpoint());
        Assert.StartsWith("SHA256:", result.Fingerprint);
        Assert.Equal($"[{Host}]:{Port} ssh-ed25519 {TestEd25519Base64()}",
            result.KeyLine);
        Assert.Equal(2, runner.Calls);
        Assert.False(runner.UnsafeOptionsObserved);
        Assert.False(File.Exists(runner.TemporaryFile));
        Assert.False(Directory.Exists(Path.GetDirectoryName(runner.TemporaryFile!)));
    }

    [Fact]
    public async Task WindowsKexFallbackWithoutValidHostKeyFailsClosed()
    {
        var runner = new KexFailureThenSshRunner(produceValidKey: false);
        var service = new SshService(runner);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ScanHostKeyAsync(Endpoint()));
        Assert.Contains("bekannter Windows-Fehler", failure.Message);
        Assert.Contains("niemals automatisch vertraut", failure.Message);
        Assert.Equal(2, runner.Calls);
        Assert.False(File.Exists(runner.TemporaryFile));
    }

    [Fact]
    public void WindowsOpenSshToolHasPriorityOverOtherImplementations()
    {
        var root = Path.Combine(Path.GetTempPath(), "thunder-ssh-" + Guid.NewGuid().ToString("N"));
        var openSsh = Path.Combine(root, "System32", "OpenSSH");
        Directory.CreateDirectory(openSsh);
        try
        {
            var bin = Path.Combine(openSsh, "ssh-keyscan.exe");
            File.WriteAllText(bin, "test placeholder");
            Assert.Equal(bin, ProcessRunner.ResolveOpenSshPath("ssh-keyscan", root));
            Assert.Equal("ssh-keygen", ProcessRunner.ResolveOpenSshPath("ssh-keygen", root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
