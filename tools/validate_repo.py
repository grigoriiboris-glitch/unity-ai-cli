#!/usr/bin/env python3
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]

REQUIRED = [
    "README.md",
    "LICENSE",
    "CONTRIBUTING.md",
    "AGENTS.md",
    ".ai/CHECKPOINT.md",
    ".ai/NEXT.md",
    ".ai/DECISIONS.md",
    ".agents/skills/unity/SKILL.md",
    ".github/workflows/ci.yml",
    "Packages/manifest.json",
    "ProjectSettings/ProjectVersion.txt",
    "Packages/com.grigoriiboris-glitch.ai-verification/package.json",
    "Packages/com.grigoriiboris-glitch.ai-verification/Runtime/UI/UIBoundsVerifier.cs",
    "Packages/com.grigoriiboris-glitch.ai-verification/Tests/PlayMode/RuntimeRegressionScenarioTests.cs",
    "Packages/com.grigoriiboris-glitch.ai-verification/Runtime/UI/UIBoundsVerifier.cs",
    "Packages/com.grigoriiboris-glitch.ai-verification/Tests/PlayMode/RuntimeRegressionScenarioTests.cs",
    "tools/diagnostics-broker/broker.py",
    "tools/diagnostics-broker/cli.py",
    "tools/diagnostics-broker/collector.py",
    "tools/diagnostics-broker/tests/test_broker.py",
]

def fail(message: str) -> None:
    print(f"ERROR: {message}")
    raise SystemExit(1)

for relative in REQUIRED:
    if not (ROOT / relative).is_file():
        fail(f"missing required file: {relative}")

version = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
if "m_EditorVersion: 6000.3.23f1" not in version:
    fail("Unity version is not pinned to 6000.3.23f1")

manifest = json.loads((ROOT / "Packages/manifest.json").read_text(encoding="utf-8"))
if manifest.get("dependencies", {}).get("com.unity.test-framework") != "1.6.0":
    fail("Unity Test Framework must be pinned to 1.6.0")

package = json.loads(
    (ROOT / "Packages/com.grigoriiboris-glitch.ai-verification/package.json").read_text(
        encoding="utf-8"
    )
)
if package.get("version") != "0.1.0":
    fail("Runtime package must start at version 0.1.0")
if package.get("unity") != "6000.3":
    fail("Runtime package Unity compatibility must be 6000.3")

print("Repository sanity: PASS")
