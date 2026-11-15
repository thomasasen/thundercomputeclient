using System.Text.RegularExpressions;

namespace ThunderClient.Core;

/// <summary>
/// Fail-closed interpretation of the upstream bootstrap's documented log events.
/// HTTP 200 is not proof that the selected model weights and custom nodes are ready.
/// An independent read-only SSH audit is still required after this gate opens.
/// </summary>
public sealed class DeploymentReadinessGate
{
    private readonly HashSet<string> _expectedModels;
    private readonly Dictionary<string, string> _modelStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _phases = new(StringComparer.Ordinal);
    private readonly string _expectedCommit;
    private bool _commitSeen;
    private bool _downloadsReportedComplete;
    private bool _dependencyAuditClean;
    private bool _httpReady;
    private string? _blocker;

    private static readonly Regex CommitLine = new(
        @"\[POD-BOOTSTRAP\] GitHub-Commit:\s*(?<commit>[a-fA-F0-9]{40})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Timing = new(
        @"\[START-TIMING\]\s+phase=(?<phase>[a-z_]+)\s+elapsed_s=[0-9.]+\s+status=(?<status>ok|error|timeout)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex FailedCount = new(
        @"\b(?:missing|conflict|unresolved|pip_issue_lines)=(?<count>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public DeploymentReadinessGate(IEnumerable<ModelWeight> models, string expectedCommit)
    {
        _expectedCommit = expectedCommit;
        _expectedModels = models.Select(model =>
            model.Source + ":" + model.Category + "/" + model.Filename)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (expectedCommit.Length != 40 || !expectedCommit.All(Uri.IsHexDigit))
            throw new ArgumentException("Ungültige geprüfte GitHub-Revision.", nameof(expectedCommit));
    }

    public int ExpectedModels => _expectedModels.Count;
    public int FinishedModels => _expectedModels.Count(model =>
        _modelStates.TryGetValue(model, out var state) &&
        state is "GELADEN" or "VORHANDEN");
    public bool HttpReady => _httpReady;
    public bool HasBlocker => _blocker is not null;
    public string? Blocker => _blocker;

    public bool ReadyForAudit => _blocker is null &&
        _commitSeen && _downloadsReportedComplete && _dependencyAuditClean && _httpReady &&
        FinishedModels == ExpectedModels &&
        new[] { "custom_nodes", "workflow_copies", "workflow_setups",
                "dependency_final_audit", "cuda_handoff_preflight" }
            .All(_phases.Contains);

    public void Observe(string line)
    {
        var commit = CommitLine.Match(line);
        if (commit.Success)
        {
            _commitSeen = true;
            if (!string.Equals(commit.Groups["commit"].Value, _expectedCommit,
                StringComparison.OrdinalIgnoreCase))
                _blocker = "Der Remote-Bootstrap verwendet einen anderen GitHub-Commit als die geprüfte Workflow-Auswahl.";
        }

        if (DownloadTracker.TryParse(line, out var transfer) && transfer is not null)
        {
            if (!_expectedModels.Contains(transfer.Model))
                _blocker ??= "Der Bootstrap meldet ein Modell außerhalb der geprüften Auswahl.";
            else
            {
                _modelStates[transfer.Model] = transfer.State;
                if (transfer.State == "FEHLER")
                    _blocker ??= "Mindestens ein erforderlicher Modelldownload ist fehlgeschlagen.";
            }
        }

        if (line.Contains("Download-Abschluss: Alle ausgewählten Modelle vorhanden oder erfolgreich geladen",
            StringComparison.Ordinal))
            _downloadsReportedComplete = true;
        if (line.Contains("Modelldownloads fehlgeschlagen; ComfyUI startet trotzdem",
            StringComparison.Ordinal))
            _blocker ??= "Ein erforderlicher Modelldownload ist fehlgeschlagen.";

        if (line.Contains("[DEPENDENCY-AUDIT] final=1 ", StringComparison.Ordinal))
        {
            _dependencyAuditClean = line.Contains("pip_check=ok", StringComparison.Ordinal) &&
                !FailedCount.Matches(line).Any(match =>
                    !int.TryParse(match.Groups["count"].Value, out var count) || count != 0);
            if (!_dependencyAuditClean)
                _blocker ??= "Die abschließende Python-/Node-Abhängigkeitsprüfung meldet Konflikte oder unklare Anforderungen.";
        }

        var timing = Timing.Match(line);
        if (timing.Success)
        {
            var phase = timing.Groups["phase"].Value;
            var status = timing.Groups["status"].Value;
            if (status == "ok")
            {
                _phases.Add(phase);
                if (phase == "comfyui_http_ready") _httpReady = true;
            }
            else if (status == "error" &&
                     phase is "custom_nodes" or "workflow_copies" or "workflow_setups" or
                         "dependency_final_audit" or "cuda_handoff_preflight")
                _blocker ??= "Eine erforderliche Installationsphase ist fehlgeschlagen: " + phase;
        }
    }

    public string ProgressDescription =>
        _blocker ?? (_httpReady
            ? "ComfyUI antwortet. Prüfe Modelle, Nodes, Installationsphasen und den vollständigen Abschluss."
            : "Installation läuft: " + FinishedModels + "/" + ExpectedModels +
              " Modelle vorhanden oder geladen; ComfyUI noch nicht vollständig bestätigt.");
}
