---
name: unity
description: Default workflow for implementing Unity changes through Coplay MCP and verifying them with unity-ai CLI. Use when modifying a Unity project, scene, prefab, script or UI.
---

# Unity action and verification workflow

## Architecture boundary
- Coplay MCP is the Action Layer: it edits scenes, GameObjects, components, scripts, assets and prefabs; controls the Editor; reads the live Console; and runs Unity tests.
- unity-ai CLI is the Verification & Guardrails Layer: it reads bounded Editor.log tails, deduplicates exceptions, analyses screenshots, reads C# JSON reports and enforces saved-scene Git checkpoints.
- C# checks requiring live engine state run in the included Unity UPM package.
- Never create another Unity MCP server, C# socket, HTTP listener or second Editor transport.

## Before changes
1. Inspect the target with Coplay MCP.
2. Read current diagnostics with:
      node packages/cli/src/index.mjs logs summarize --include-source
3. Create/checkout a task branch.
4. Before a risky change, invoke Coplay MCP execute_menu_item with menu_path:
      Tools/AI Verification/Save Scene and Assets
5. Create a checkpoint:
      node packages/cli/src/index.mjs checkpoint create before-<task>

## After changes
1. Use Coplay MCP to select affected GameObjects.
2. For UI pixel bounds, enter Play Mode and ensure Camera.main is active.
3. Invoke:
      execute_menu_item(menu_path="Tools/AI Verification/Run Selected Checks")
4. Run:
      node packages/cli/src/index.mjs verify --suite logs,physics,ui
5. Inspect failures, repair through Coplay MCP, and repeat.
6. Run targeted Unity tests through Coplay MCP run_tests and poll get_test_job.
7. Before the final checkpoint, invoke Tools/AI Verification/Save Scene and Assets again.
8. Create a final checkpoint only after the CLI accepts the fresh save-state file.
9. Require green GitHub Actions before marking the work complete.

## Output discipline
Use compact JSON summaries. Request full stack traces only for selected error IDs. Do not paste Editor.log or large screenshot sequences into the AI context.
