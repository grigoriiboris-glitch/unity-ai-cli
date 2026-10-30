---
name: unity-self-check
description: Run targeted Unity compile, diagnostics, physics, UI and runtime verification after changes made through Coplay MCP. Use before declaring a Unity task complete.
---

# Unity self-check

## Preconditions
- Work from the Unity repository root.
- Use the already configured Coplay MCP. Do not start or install a second MCP server.
- Select only GameObjects relevant to the task. Broad scene scans produce noisy physics and visibility findings.

## Workflow
1. Use Coplay MCP read_console or the bounded CLI log summary to inspect existing compile errors before making changes.
2. Apply edits and scene changes using Coplay MCP.
3. If UI pixel bounds are part of the task, enter Play Mode and ensure Camera.main is active.
4. In Coplay MCP call execute_menu_item with menu_path exactly equal to:
   Tools/AI Verification/Run Selected Checks
5. From the Unity repository root run:
      node packages/cli/src/index.mjs verify --suite logs,physics,ui
   Supply --editor-log <path> when Editor.log is not at the detected OS default.
6. Read the JSON result. Fix every error-level anomaly and every compile/runtime error before success.
7. If the report says blocked because UI requires Play Mode, enter Play Mode and repeat step 4.
8. Run Unity tests through Coplay MCP run_tests when the change affects runtime behavior. Poll with get_test_job.
9. Create a checkpoint only after saving through the save skill.

## Interpretation
- passed: requested checks completed without error-level anomalies or Editor.log errors.
- failed: fix the listed diagnostics and rerun.
- blocked: missing selection, stale report or required Play Mode. Do not call it passed.
- not_applicable: the selected objects do not use that capability; this is not an error by itself.

Do not paste full Editor.log into model context. Ask the CLI for grouped diagnostics, then request a single detail/stack trace only when necessary.
