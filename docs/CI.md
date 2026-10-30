# CI

## Mandatory Node.js checks

Every pull request must pass:
- Repository contract validation.
- JavaScript syntax checks.
- Node.js regression tests for SQLite diagnostics, Editor.log parsing/deduplication, source excerpts, image diff and frame selection.

The workflow uses Node.js 24.15.0 or newer. node:sqlite is the built-in SQLite API; no external database process or Python runtime is required.

The CLI dependencies are pinned in packages/cli/package.json:
- sharp for image decoding and diff artifact writing.
- pixelmatch for perceptual pixel comparison.

## Unity execution

The Unity job is always present but opt-in:

UNITY_CI_ENABLED=true

Repository secrets required when enabled:
- UNITY_EMAIL
- UNITY_PASSWORD
- UNITY_SERIAL

The job runs Unity 6000.3.23f1 and all EditMode/PlayMode tests through pinned game-ci/unity-test-runner v4.4.0.

Without the variable enabled, the Unity job is skipped. Coplay MCP can still execute targeted Unity tests interactively.
