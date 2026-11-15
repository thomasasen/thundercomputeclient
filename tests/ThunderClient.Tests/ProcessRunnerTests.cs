using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void InstalledThunderCliIsFoundOutsideGuiPath()
    {
        var temp = Path.Combine(Path.GetTempPath(), "thunder-cli-" + Guid.NewGuid().ToString("N"));
        var programFilesX86 = Path.Combine(temp, "Program Files (x86)");
        var exeFolder = Path.Combine(programFilesX86, "tnr");
        Directory.CreateDirectory(exeFolder);
        var executable = Path.Combine(exeFolder, "tnr.exe");
        try
        {
            File.WriteAllText(executable, "dummy exe used only for path resolution");
            Assert.Equal(executable, ProcessRunner.ResolveThunderCliPath(
                searchPath: "", programFilesX86: programFilesX86,
                programFiles: Path.Combine(temp, "other")));
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public void ThunderCliFromCurrentPathTakesPrecedence()
    {
        var temp = Path.Combine(Path.GetTempPath(), "thunder-cli-" + Guid.NewGuid().ToString("N"));
        var bin = Path.Combine(temp, "bin");
        Directory.CreateDirectory(bin);
        var executable = Path.Combine(bin, "tnr.exe");
        try
        {
            File.WriteAllText(executable, "dummy exe used only for path resolution");
            Assert.Equal(executable, ProcessRunner.ResolveThunderCliPath(
                searchPath: bin, programFilesX86: Path.Combine(temp, "other"),
                programFiles: Path.Combine(temp, "other2")));
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public void UnresolvableThunderCliFallsBackToSystemError()
    {
        var temp = Path.Combine(Path.GetTempPath(), "thunder-no-cli-" + Guid.NewGuid().ToString("N"));
        Assert.Equal("tnr", ProcessRunner.ResolveThunderCliPath(
            searchPath: "", programFilesX86: temp, programFiles: temp));
    }
}
