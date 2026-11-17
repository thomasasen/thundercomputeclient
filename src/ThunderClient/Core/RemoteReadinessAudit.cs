using System.Text.RegularExpressions;

namespace ThunderClient.Core;

public sealed record RemoteReadinessResult(bool Ready, int Models, int Nodes, string Reason);

/// <summary>
/// Read-only verification on an existing SSH-authenticated Thunder pod.
/// Uses the exact upstream manifest resolver from the inspected GitHub revision.
/// Nothing is installed, downloaded, deleted, or exposed to the Internet.
/// </summary>
public static class RemoteReadinessAudit
{
    private static readonly Regex Commit = new(
        @"\A[a-fA-F0-9]{40}\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex WorkflowNames = new(
        @"\A[A-Za-z0-9_.-]+(?:,[A-Za-z0-9_.-]+)*\z",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Result = new(
        @"^\[THUNDER-READINESS\] status=(?<status>ready|blocked)(?: models=(?<models>\d+) nodes=(?<nodes>\d+)| reason=(?<reason>[a-z0-9-]+) count=(?<count>\d+))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static string BuildScript(string commit, string workflowCsv)
    {
        if (!Commit.IsMatch(commit) || !WorkflowNames.IsMatch(workflowCsv))
            throw new ArgumentException("Ungültige GitHub-Revision oder Workflow-Auswahl für den Remote-Check.");

        // No model or provider tokens go into this script. The only remote
        // network probes are ComfyUI's loopback-only read-only HTTP endpoints.
        return "set -eu\n" +
               "export THUNDER_AUDIT_COMMIT=" + SshService.QuoteShell(commit.ToLowerInvariant()) + "\n" +
               "export THUNDER_AUDIT_WORKFLOWS=" + SshService.QuoteShell(workflowCsv) + "\n" +
               "python3 - <<'PY'\n" + PythonAudit + "\nPY\n";
    }

    public static RemoteReadinessResult Parse(CommandResult output)
    {
        foreach (var line in output.Stdout.Split('\n', StringSplitOptions.TrimEntries))
        {
            var match = Result.Match(line);
            if (!match.Success) continue;
            if (match.Groups["status"].Value == "ready" && output.Success &&
                int.TryParse(match.Groups["models"].Value, out var models) &&
                int.TryParse(match.Groups["nodes"].Value, out var nodes))
                return new RemoteReadinessResult(true, models, nodes, "");
            if (match.Groups["status"].Value == "blocked")
                return new RemoteReadinessResult(false, 0, 0,
                    match.Groups["reason"].Value + " (" + match.Groups["count"].Value + ")");
        }
        return new RemoteReadinessResult(false, 0, 0,
            "Der Remote-Vollständigkeitscheck lieferte keinen gültigen Abschlussstatus.");
    }

    private const string PythonAudit = """
import json
import os
from pathlib import Path
import sys
import urllib.request


def blocked(reason, count=0):
    print(f'[THUNDER-READINESS] status=blocked reason={reason} count={count}', flush=True)
    raise SystemExit(2)


commit = os.environ['THUNDER_AUDIT_COMMIT']
workspace = Path.home() / 'comfyui-workspace'
root = workspace / '.runpod-ai-workflows' / commit
base = workspace / 'comfyui'
if not (root / 'scripts' / 'manifest.py').is_file() or root.is_symlink():
    blocked('pinned-snapshot-missing')
sys.path.insert(0, str(root / 'scripts'))
try:
    from manifest import resolve, selected
    names = selected(os.environ['THUNDER_AUDIT_WORKFLOWS'])
    manifests, models, nodes = resolve(root, names)
except (ValueError, OSError, ImportError):
    blocked('manifest-unavailable')
if not names or len(manifests) != len(names):
    blocked('workflow-selection-mismatch')

report = base / 'download_failures.json'
if report.is_symlink() or not report.is_file():
    blocked('download-report-missing')
try:
    failures = json.loads(report.read_text(encoding='utf-8'))['failed_downloads']
except (OSError, ValueError, KeyError, TypeError):
    blocked('download-report-invalid')
if not isinstance(failures, list):
    blocked('download-report-invalid')
if failures:
    blocked('failed-downloads', len(failures))

missing = 0
for model in models:
    destination = base / 'models' / model['category'] / model['filename']
    if destination.is_symlink():
        missing += 1
        continue
    if model['source'] == 'hf_snapshot':
        files = model.get('files', [])
        if not destination.is_dir() or not files or any(
            (destination / name).is_symlink() or
            not (destination / name).is_file() or
            (destination / name).stat().st_size == 0 for name in files
        ):
            missing += 1
    elif not destination.is_file() or destination.stat().st_size == 0:
        missing += 1
if missing:
    blocked('missing-model-files', missing)

missing = 0
for node in nodes:
    path = base / 'custom_nodes' / node['name']
    if path.is_symlink() or not path.is_dir():
        missing += 1
        continue
    if 'archive' in node:
        marker = path / '.runpod-node-archive.json'
        if marker.is_symlink() or not marker.is_file():
            missing += 1
            continue
        try:
            recorded = json.loads(marker.read_text(encoding='utf-8'))
        except (OSError, ValueError):
            recorded = None
        if recorded != {'archive': node['archive'], 'workflow': node.get('_workflow')}:
            missing += 1
    else:
        marker = path / '.runpod-node-commit'
        if marker.is_symlink() or not marker.is_file() or (
            marker.read_text(encoding='utf-8').strip().lower() != node['commit'].lower()
        ):
            missing += 1
if missing:
    blocked('missing-pinned-nodes', missing)

missing = 0
for name, doc in manifests:
    variants = [name] + [v['name'] for v in doc.get('workflow_variants', [])]
    for canvas in variants:
        file = base / 'imported_workflows' / (canvas + '.json')
        if file.is_symlink() or not file.is_file() or file.stat().st_size == 0:
            missing += 1
if missing:
    blocked('missing-workflow-canvases', missing)

try:
    def read_json(path):
        with urllib.request.urlopen('http://127.0.0.1:8188/' + path, timeout=15) as response:
            if response.status != 200:
                blocked('comfy-http-not-ready')
            raw = response.read(25000001)
            if len(raw) > 25000000:
                blocked('comfy-response-too-large')
            return json.loads(raw)

    system = read_json('system_stats')
    classes = read_json('object_info')
except (OSError, TimeoutError, ValueError, TypeError):
    blocked('comfy-http-not-ready')
if not isinstance(system, dict) or not isinstance(classes, dict) or not classes:
    blocked('comfy-object-info-invalid')

# A custom-node folder may exist but still fail to import at ComfyUI startup.
# Verify that the running backend exposes at least one registered class from
# every selected custom-node package. Missing mappings block full readiness.
def normalize(value):
    return ''.join(ch for ch in str(value).lower() if ch.isalnum())

loaded = set()
for details in classes.values():
    if isinstance(details, dict):
        module = str(details.get('python_module', ''))
        if module.startswith('custom_nodes.'):
            loaded.add(normalize(module.split('.')[1]))
unregistered = sum(
    not any(module == normalize(node['name']) for module in loaded)
    for node in nodes
)
if unregistered:
    blocked('node-classes-unverified', unregistered)

# Package registration is not enough: selected workflow graphs can reference
# a missing individual node class even when other classes from that package
# loaded successfully. Compare every executable graph type to the live API.
frontend_only = {'Reroute', 'PrimitiveNode', 'Note', 'MarkdownNote', 'SetNode', 'GetNode'}
missing_classes = set()
for name, doc in manifests:
    files = [doc.get('workflow_file', 'workflow.json')] + [
        item['file'] for item in doc.get('workflow_variants', [])
    ]
    for filename in files:
        path = root / 'workflows' / name / filename
        try:
            graph = json.loads(path.read_text(encoding='utf-8'))
        except (OSError, ValueError):
            blocked('workflow-graph-invalid')
        if isinstance(graph, dict) and isinstance(graph.get('nodes'), list):
            used = {item.get('type') for item in graph['nodes'] if isinstance(item, dict)}
        elif isinstance(graph, dict):
            used = {item.get('class_type') for item in graph.values()
                    if isinstance(item, dict)}
        else:
            blocked('workflow-graph-invalid')
        for kind in used:
            if isinstance(kind, str) and kind not in classes and (
                kind not in frontend_only and 'reroute' not in kind.casefold()
            ):
                missing_classes.add(kind)
if missing_classes:
    blocked('missing-workflow-node-classes', len(missing_classes))

print(f'[THUNDER-READINESS] status=ready models={len(models)} nodes={len(nodes)}', flush=True)
""";
}
