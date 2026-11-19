using System.ComponentModel;
using ThunderClient.Core;
using ThunderClient.UI;
using Xunit;

namespace ThunderClient.Tests;

public sealed class DownloadTablePresentationTests
{
    [Theory]
    [InlineData("4,5 GiB (9%)", 9d)]
    [InlineData("1.8 GiB (23.75%)", 23.75)]
    [InlineData("Download (100%)", 100d)]
    [InlineData("3 GiB / 8 GiB", null)]
    [InlineData("Keine belastbaren Transferdaten", null)]
    [InlineData("Unzulässig (125%)", null)]
    public void NumericSortingUsesOnlyReportedValidPercentages(
        string progress, double? expected)
    {
        var item = new DownloadItem("test.safetensors", "FORTSCHRITT", progress);
        if (expected is null)
            Assert.Null(item.ProgressSortValue);
        else
            Assert.Equal((decimal)expected.Value, item.ProgressSortValue);
    }

    [Fact]
    public void ProgressSortNotifiesLiveViewOnLogUpdates()
    {
        var item = new DownloadItem("a.safetensors", "LAEUFT", "(10%)");
        var updated = new List<string?>();
        item.PropertyChanged += (_, e) => updated.Add(e.PropertyName);
        item.Progress = "(90%)";
        item.State = "FORTSCHRITT";
        Assert.Equal(90m, item.ProgressSortValue);
        Assert.Contains(nameof(DownloadItem.ProgressSortValue), updated);
        Assert.Contains(nameof(DownloadItem.State), updated);
        Assert.Equal("a.safetensors", item.Model);
    }

    [Theory]
    [InlineData("Bootstrap manuell starten …", "play")]
    [InlineData("Git aktualisieren & ComfyUI neu starten …", "refresh")]
    [InlineData("Instanz löschen …", "delete")]
    [InlineData("Einstellungen speichern", "save")]
    [InlineData("Katalog aktualisieren", "refresh")]
    [InlineData("Übersicht", "dashboard")]
    [InlineData("WARTET", "schedule")]
    [InlineData("LAEUFT", "sync")]
    [InlineData("FORTSCHRITT", "sync")]
    [InlineData("RETRY", "warning")]
    [InlineData("GELADEN", "check_circle")]
    [InlineData("VORHANDEN", "check_circle")]
    [InlineData("FEHLER", "error")]
    [InlineData("Erfolgreich getestet", "check_circle")]
    [InlineData("Fehlt", "error")]
    public void SemanticMaterialIconMappingRemainsDeterministic(
        string label, string expected)
    {
        Assert.Equal(expected, MaterialIconConverter.IconForLabel(label));
    }

    [Fact]
    public void UnknownActionHasNoMisleadingFallbackSymbol()
    {
        Assert.Null(MaterialIconConverter.IconForLabel("Unbekannte Aktion"));
    }
}
