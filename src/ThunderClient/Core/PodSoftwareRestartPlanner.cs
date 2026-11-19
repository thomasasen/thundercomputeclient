using System.Text.RegularExpressions;

namespace ThunderClient.Core;

/// <summary>
/// Fail-closed validation before modifying a running application. The GitHub
/// snapshot is freshly fetched and commit-pinned; no stale/offline fallback is
/// permitted for a requested update.
/// </summary>
public static class PodSoftwareRestartPlanner
{
    public static IReadOnlyList<WorkflowItem> Validate(
        WorkflowSnapshot latest, IReadOnlyList<string> workflowNames)
    {
        if (!Regex.IsMatch(latest.Commit, @"\A[a-fA-F0-9]{40}\z"))
            throw new InvalidOperationException("Neue GitHub-Revision ist nicht eindeutig bestätigt.");
        if (workflowNames.Count == 0 ||
            workflowNames.Any(name => !Regex.IsMatch(name, @"\A[A-Za-z0-9][A-Za-z0-9_.-]*\z")) ||
            workflowNames.Distinct(StringComparer.Ordinal).Count() != workflowNames.Count)
            throw new InvalidOperationException("Keine gültige, eindeutige Workflow-Auswahl für den Neustart.");

        var selected = new List<WorkflowItem>(workflowNames.Count);
        foreach (var name in workflowNames)
        {
            var workflow = latest.Workflows.SingleOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.Ordinal));
            if (workflow is null)
                throw new InvalidOperationException(
                    "Workflow " + name + " existiert in der aktuellen GitHub-Revision nicht mehr. " +
                    "Nichts wird auf dem Pod gestoppt; Workflow-Auswahl prüfen.");
            if (!string.IsNullOrWhiteSpace(workflow.RequiredAuthorizationEnv))
                throw new InvalidOperationException(
                    "Workflow " + name + " verlangt eine separate Modellfreigabe. " +
                    "Automatischer Neustart gesperrt, bis die Berechtigung ausdrücklich geprüft ist.");
            selected.Add(workflow);
        }

        if (WorkflowCatalog.Estimate(selected).Collisions != 0)
            throw new InvalidOperationException(
                "Die neuen Workflow-Manifeste enthalten widersprüchliche Modell-Zieldateien. " +
                "Der laufende Pod bleibt unverändert.");
        return selected;
    }
}
