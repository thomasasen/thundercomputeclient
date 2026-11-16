"""Execute the production remote Python readiness audit against local fixtures.

No Thunder account, network service, GPU, provider requests or model downloads.
The actual Python body is read from the compiled-in C# raw-string source.
"""
from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src/ThunderClient/Core/RemoteReadinessAudit.cs"
COMMIT = "b5276af36dd0ccd4477f49de86e4a120a8f8ac54"
NODE_PIN = "a" * 40


def production_audit() -> str:
    raw = SOURCE.read_text(encoding="utf-8")
    return raw.split('private const string PythonAudit = """\n', 1)[1].split('\n""";', 1)[0]


RUNNER = r"""
import json
import os
import pathlib
import urllib.request

real_home = pathlib.Path(os.environ['TEST_HOME'])
pathlib.Path.home = staticmethod(lambda: real_home)

class Response:
    status = 200
    def __init__(self, data):
        self._data = json.dumps(data).encode("utf-8")
    def __enter__(self): return self
    def __exit__(self, kind, value, tb): return False
    def read(self, count): return self._data[:count]

def fakeopen(url, timeout=0):
    if url.endswith('/system_stats'):
        return Response({"system": {"comfyui_version": "test"}})
    if url.endswith('/object_info'):
        classes = {
            "LoadImage": {"python_module": "nodes"},
            "MagicNode": {"python_module": "custom_nodes.TestPack"}
        }
        if os.environ.get('REMOVE_CLASS') == '1':
            del classes['MagicNode']
        return Response(classes)
    raise ValueError("Unregistered endpoint")

urllib.request.urlopen = fakeopen
exec(compile(os.environ['AUDIT_SCRIPT'], "embedded_readiness", "exec"))
"""

MOCK_MANIFEST = """def selected(value):
    return value.split(",")

def resolve(root, names):
    return (
        [(name, {"workflow_file": "workflow.json", "workflow_variants": []})
         for name in names],
        [{"source": "hf", "category": "checkpoints", "filename": "weight.safetensors"}],
        [{"name": "TestPack", "commit": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]
    )
"""


class RemoteReadinessFixtureTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.home = Path(self.directory.name)
        workspace = self.home / "comfyui-workspace"
        root = workspace / ".runpod-ai-workflows" / COMMIT
        (root / "scripts").mkdir(parents=True)
        (root / "scripts" / "manifest.py").write_text(
            MOCK_MANIFEST, encoding="utf-8")
        canvas = root / "workflows" / "Demo"
        canvas.mkdir(parents=True)
        (canvas / "workflow.json").write_text(
            json.dumps({"nodes": [
                {"type": "LoadImage"}, {"type": "MagicNode"}]}), encoding="utf-8")
        base = workspace / "comfyui"
        self.model = base / "models" / "checkpoints" / "weight.safetensors"
        self.model.parent.mkdir(parents=True)
        self.model.write_bytes(b"FAKE MODEL FOR TEST ONLY")
        (base / "download_failures.json").write_text(
            '{"failed_downloads":[]}', encoding="utf-8")
        node = base / "custom_nodes" / "TestPack"
        node.mkdir(parents=True)
        (node / ".runpod-node-commit").write_text(NODE_PIN, encoding="utf-8")
        imported = base / "imported_workflows"
        imported.mkdir()
        (imported / "Demo.json").write_text(
            '{"nodes":[{"type":"MagicNode"}]}', encoding="utf-8")

    def run_audit(self, *, missing_class=False):
        environment = dict(os.environ)
        environment.update(
            TEST_HOME=str(self.home),
            THUNDER_AUDIT_COMMIT=COMMIT,
            THUNDER_AUDIT_WORKFLOWS="Demo",
            REMOVE_CLASS="1" if missing_class else "0",
            AUDIT_SCRIPT=production_audit(),
            PYTHONDONTWRITEBYTECODE="1",
        )
        return subprocess.run(
            [sys.executable, "-c", RUNNER], env=environment,
            text=True, capture_output=True, timeout=20, check=False)

    def test_successful_file_and_registered_node_check(self):
        result = self.run_audit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            "[THUNDER-READINESS] status=ready models=1 nodes=1", result.stdout)

    def test_missing_or_empty_weight_is_blocked(self):
        self.model.write_bytes(b"")
        result = self.run_audit()
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn("reason=missing-model-files", result.stdout)

    def test_installed_but_unregistered_node_blocks(self):
        result = self.run_audit(missing_class=True)
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn("reason=node-classes-unverified", result.stdout)

    def test_upstream_failure_report_blocks(self):
        report = self.home / "comfyui-workspace" / "comfyui" / "download_failures.json"
        report.write_text('{"failed_downloads":[{"filename":"weight.safetensors"}]}',
                          encoding="utf-8")
        result = self.run_audit()
        self.assertEqual(result.returncode, 2, result.stderr)
        self.assertIn("reason=failed-downloads", result.stdout)


if __name__ == "__main__":
    unittest.main()
