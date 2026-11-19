using ThunderClient.Core;
using System.Text.RegularExpressions;

namespace ThunderClient.Core
{
    // Integration harness compiles the real supervisor class without bringing
    // the Windows-specific SSH transport into an Ubuntu test process.
    internal static class SshService
    {
        public static string PinBootstrapAdapter(string text, string sha)
        {
            if (!Regex.IsMatch(sha, @"\A[a-f0-9]{40}\z"))
                throw new ArgumentException("bad sha");
            return text;
        }

        public static string QuoteShell(string text) =>
            "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("launch, restart or follow");
        const string demo = "#!/usr/bin/env bash\n" +
            "if [[ \"$GITHUB_TOKEN\" != 'gh-demo' ]]; then exit 30; fi\n" +
            "echo '[TEST-REMOTE] started'\n" +
            "sleep 4\n" +
            "echo '[TEST-REMOTE] completed'\n";
        var output = args[0] switch
        {
            "launch" => RemoteBootstrapSupervisor.BuildLaunchScript(
                demo, new string('a', 40), "Demo", "gh-demo", "", ""),
            "restart" => RemoteBootstrapSupervisor.BuildLaunchScript(
                demo, new string('a', 40), "Demo", "gh-demo", "", "", restart: true),
            "follow" => RemoteBootstrapSupervisor.BuildFollowScript(),
            _ => throw new ArgumentException("unknown action")
        };
        Console.Write(output);
    }
}
