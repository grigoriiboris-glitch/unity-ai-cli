# Unity AI CLI

**Verification & Guardrails Layer for Pi.dev + Coplay MCP.**

The existing Coplay MCP remains the only Action Layer for Unity Editor. This repository does not implement another MCP server or another Unity transport.

## Architecture

- **Coplay MCP** edits scenes, GameObjects, components, prefabs, assets and scripts; runs Unity tests; and invokes Editor menu items.
- **Node.js CLI** reads Unity logs and JSON reports, deduplicates repeated failures, compares screenshots, manages local Git checkpoints and returns compact JSON for Pi.dev.
- **Unity UPM package (C#)** runs checks that require live engine state and writes reports under .ai/artifacts/.
- **Pi.dev Skills** define the safe action → save → verify → repair → checkpoint loop.

## Requirements

- Node.js >= 24.15.0.
- Unity 6000.3.23f1 for the included UPM package.
- Coplay MCP installed and connected to the Unity Editor.
- The CLI uses the built-in node:sqlite module and does not need a separate database service.

## Install the CLI

    npm --prefix packages/cli install

Run from the Unity repository root:

    node packages/cli/src/index.mjs --help
    node packages/cli/src/index.mjs validate
    node packages/cli/src/index.mjs diagnostics task
    node packages/cli/src/index.mjs logs summarize --include-source
    node packages/cli/src/index.mjs verify --suite logs,physics,ui

The package exposes the unity-ai executable after installation.

## Unity Editor bridge

The package is already part of this Unity project. It adds two menu items:

1. Tools/AI Verification/Save Scene and Assets — saves open scenes, prefab stage and assets, then writes .ai/artifacts/editor-save-state.json.
2. Tools/AI Verification/Run Selected Checks — checks selected GameObjects and writes .ai/artifacts/verification-report.json.

Invoke these menu items through the existing Coplay MCP execute_menu_item tool. The C# bridge is file-based and does not host a server.

UI pixel-bound checks require Play Mode and a live Camera.main. Select only the objects relevant to the current task before running checks.

## Git checkpoint

A checkpoint requires a successful, recent Editor save confirmation:

    node packages/cli/src/index.mjs checkpoint create before-layout-refactor
    node packages/cli/src/index.mjs checkpoint list

The command refuses to proceed when Unity reports dirty scenes/assets or the save confirmation is older than 10 minutes. When there are pending repository changes it creates a local commit and an annotated ai-checkpoint/* tag. It never pushes.

## Skills

Pi.dev automatically discovers the project skills in .agents/skills/:

- unity — default action/save/verify workflow.
- unity-self-check — compile, diagnostics and selected-object verification.
- unity-fix-compile-errors — bounded compile-log triage and repair.
- unity-visual-qa — screenshot selection and pixel diff.

Use /skill:unity-self-check, /skill:unity-fix-compile-errors or /skill:unity-visual-qa to invoke a workflow explicitly.

## CI

GitHub Actions runs repository validation, JavaScript syntax checks and Node regression tests. Unity compile/EditMode/PlayMode tests remain opt-in through UNITY_CI_ENABLED=true and Unity license secrets.

## Scope boundary

Do not reimplement Coplay MCP features such as scene editing, GameObject CRUD, component management, asset operations, Editor navigation or test-runner transport. Add a CLI or C# check only when it provides verification/evidence Coplay does not already own.
