using System.Text.RegularExpressions;

namespace ThunderClient.Core;

/// <summary>
/// Thunder does not expose a documented in-place instance restart.
/// Recovery is a snapshot of the existing persistent disk followed by a
/// separately confirmed restore as a NEW instance. Never delete or replace
/// the existing GPU instance as part of preparing the snapshot.
/// </summary>
public static class PodRecoveryPlanner
{
    public const string ConsoleUrl = "https://console.thundercompute.com/";

    public static InstanceItem RequireSameRunningInstance(
        InstanceItem selected, IReadOnlyList<InstanceItem> current)
    {
        if (!Regex.IsMatch(selected.Id, @"\A[A-Za-z0-9_-]{1,64}\z"))
            throw new InvalidOperationException("Keine gültige Instanz-ID ausgewählt.");
        var matches = current.Where(item =>
            string.Equals(item.Id, selected.Id, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                "Die ausgewählte Instanz wurde in der aktuellen Thunder-Statusliste " +
                "nicht eindeutig gefunden. Es wurde kein Snapshot erstellt.");
        var fresh = matches[0];
        if (fresh.Status != "RUNNING")
            throw new InvalidOperationException(
                "Die ausgewählte Instanz ist nicht RUNNING. Snapshot nicht gestartet.");
        if (!string.IsNullOrWhiteSpace(selected.Uuid) &&
            !string.Equals(fresh.Uuid, selected.Uuid, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Instanz-UUID hat sich geändert. Snapshot zum Schutz der Daten nicht gestartet.");
        return fresh;
    }

    public static string RecoverySnapshotName(InstanceItem selected,
        DateTimeOffset utcTime, string uniqueSuffix)
    {
        if (!Regex.IsMatch(selected.Id, @"\A[A-Za-z0-9_-]{1,64}\z"))
            throw new ArgumentException("Ungültige Instanz-ID.", nameof(selected));
        if (!Regex.IsMatch(uniqueSuffix, @"\A[a-fA-F0-9]{6}\z"))
            throw new ArgumentException("Snapshot-Suffix muss aus sechs Hex-Zeichen bestehen.", nameof(uniqueSuffix));
        var shortId = selected.Id[..Math.Min(12, selected.Id.Length)];
        return $"recovery-{shortId}-{utcTime.UtcDateTime:yyyyMMddHHmmss}-{uniqueSuffix.ToLowerInvariant()}";
    }
}
