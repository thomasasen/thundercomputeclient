using System.Net;

namespace ThunderClient.Core;

/// <summary>
/// A conservative plan based only on selected manifest sizes, README RunPod
/// reference classes, and user-approved local Thunder candidates.
/// No price, driver or availability value here is a live provider quote.
/// </summary>
public sealed record AutoProvisionPlan(
    IReadOnlyList<string> WorkflowNames, string Revision,
    int DiskGb, int GpuCount, string Template,
    decimal MaxEstimatedHourlyUsd, IReadOnlyList<GpuChoice> Candidates,
    double ModelGb, int? ReferenceVramGb, string Warning)
{
    public decimal FirstHourlyEstimate =>
        Candidates.Count == 0 ? 0 :
        Estimates.Hourly(Candidates[0], GpuCount, DiskGb);
}

public static class AutoProvisioning
{
    public static AutoProvisionPlan Plan(IReadOnlyList<WorkflowItem> workflows,
        string commit, IReadOnlyList<GpuChoice> choices, int gpuCount,
        int diskGb, string template, decimal maxHourly)
    {
        if (workflows.Count == 0)
            throw new InvalidOperationException("Mindestens einen Workflow auswählen.");
        var restricted = workflows
            .Where(x => !string.IsNullOrWhiteSpace(x.RequiredAuthorizationEnv))
            .Select(x => x.Name).ToArray();
        if (restricted.Length > 0)
            throw new InvalidOperationException(
                "Diese Workflows verlangen laut Manifest eine zusätzliche ausdrückliche " +
                "Autorisierungsbestätigung, die der Desktop-Client nicht automatisch setzen darf: " +
                string.Join(", ", restricted) + ". Zuerst unter den Workflow-READMEs prüfen.");
        if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Private GitHub-Revision nicht bestätigt. Katalog erneut laden.");

        var estimate = WorkflowCatalog.Estimate(workflows);
        if (estimate.Collisions > 0)
            throw new InvalidOperationException("Modelldateien der Auswahl kollidieren. Auswahl korrigieren.");
        if (estimate.TotalGb is null)
            throw new InvalidOperationException("Einige Modelldateigrößen fehlen. Automatische Disk-Planung nicht sicher; manuell prüfen.");
        if (gpuCount is < 1 or > 8 || diskGb is < 50 or > 2000 ||
            maxHourly <= 0 || string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException("GPU-Anzahl, Disk, Template oder Preisgrenze ungültig.");
        var hardware = HardwareRecommendations.Plan(workflows);
        if (!hardware.SuggestedDiskGb.HasValue)
            throw new InvalidOperationException("Keine belastbare Disk-Planung aus Manifesten möglich.");

        // The user-selected disk is a lower bound. Never silently shrink storage.
        var plannedDisk = Math.Max(diskGb, hardware.SuggestedDiskGb.Value);
        if (plannedDisk > 2000)
            throw new InvalidOperationException(
                "Berechnete Disk über 2000 GB; erst Speicherplanung manuell überprüfen.");

        // Respect explicit GPU checkboxes. Only candidates meeting the RunPod
        // VRAM orientation can be considered; never raise a cost ceiling.
        var candidates = choices.Where(x => x.Enabled &&
            x.Vcpus > 0 &&
            (!hardware.ReferenceVramGb.HasValue ||
             x.VramGb >= hardware.ReferenceVramGb.Value) &&
            Estimates.Hourly(x, gpuCount, plannedDisk) <= maxHourly)
            .Select(x => new GpuChoice(x.Key, x.Label, x.VramGb,
                x.Vcpus, x.GpuHourlyEstimate) { Enabled = true }).ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Keine aktivierte GPU erfüllt die dokumentierte VRAM-Orientierung " +
                "und die gewählte Schätzpreisgrenze. Unter Neue Instanz GPU/Preisgrenze prüfen.");

        var warning = "GPU-/VRAM-Werte stammen aus RunPod-READMEs, keine Thunder-Livemessung. " +
            "Disk = deduplizierte Modelle + pauschale Reserve und ggf. größere RunPod-Referenz. " +
            "Kosten sind historische Schätzwerte, die Preisgrenze ist keine Abrechnungssperre. " +
            "Der Client pinnt den ausgeführten Remote-Entrypoint an die zuvor bestätigte GitHub-Revision.";
        if (!hardware.CompleteVramReference)
            warning += " Nicht für alle Workflows liegen GPU-Referenzen vor.";
        if (estimate.FilesWithoutSize > 0)
            warning += " Einige Modelldateigrößen waren unvollständig.";

        return new AutoProvisionPlan(workflows.Select(x => x.Name).ToArray(),
            commit, plannedDisk, gpuCount, template, maxHourly,
            candidates, estimate.TotalGb.Value, hardware.ReferenceVramGb, warning);
    }

    public static async Task<InstanceItem> WaitForRunningSshAsync(
        string instanceId,
        Func<CancellationToken, Task<IReadOnlyList<InstanceItem>>> list,
        Func<TimeSpan, CancellationToken, Task> delay,
        Action<string>? progress = null,
        int maxChecks = 90, bool tolerateMissing = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("Instanz-ID fehlt.", nameof(instanceId));
        if (maxChecks is < 1 or > 180)
            throw new ArgumentOutOfRangeException(nameof(maxChecks));

        for (var i = 0; i < maxChecks; i++)
        {
            ct.ThrowIfCancellationRequested();
            var items = await list(ct);
            var pod = items.SingleOrDefault(x => x.Id == instanceId);
            if (pod is null)
            {
                if (!tolerateMissing || i == maxChecks - 1)
                    throw new InvalidOperationException(
                        "Pod " + instanceId + " noch nicht im Thunder-Status gefunden. " +
                        "Er kann bereits kostenpflichtig laufen. Keine neue Instanz wird automatisch angelegt.");
                progress?.Invoke("Pod " + instanceId +
                    ": Create-Identität gesichert; Statusliste noch nicht konsistent (" +
                    (i + 1) + "/" + maxChecks + ").");
                await delay(TimeSpan.FromSeconds(4), ct);
                continue;
            }
            if (pod.Status is "STOPPED" or "TERMINATED" or "ERROR" or "FAILED")
                throw new InvalidOperationException(
                    "Pod " + instanceId + " ist nicht mehr startbar (" + pod.Status +
                    "). Keine weiteren Create-Versuche.");
            if (pod.Status == "RUNNING" &&
                IPAddress.TryParse(pod.Ip, out _) && pod.Port is >= 1 and <= 65535)
                return pod;
            progress?.Invoke("Pod " + instanceId + ": " + pod.Status +
                "; SSH-IP/Port noch nicht bereit (" + (i + 1) + "/" + maxChecks + ").");
            if (i < maxChecks - 1)
                await delay(TimeSpan.FromSeconds(4), ct);
        }
        throw new TimeoutException(
            "Pod wurde nicht rechtzeitig mit erreichbarem SSH-Endpunkt gemeldet. " +
            "Der Pod existiert möglicherweise weiterhin und verursacht Kosten. " +
            "Keine weitere Instanz anlegen; Status unter Instanzen prüfen.");
    }
}
