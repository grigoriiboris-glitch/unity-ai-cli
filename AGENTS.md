# AGENTS.md — Unity Verification & Guardrails

## Mission
Build a deterministic verification layer for Pi.dev-driven Unity work. The existing Coplay MCP owns all Unity Editor actions. This repository must not create a second MCP server, WebSocket, HTTP endpoint or editor transport.

## Responsibility boundaries
- Coplay MCP: inspect and modify scenes, GameObjects, components, assets, prefabs, scripts and Editor state; invoke menu items and tests.
- Node.js CLI (packages/cli): inspect files/logs, deduplicate diagnostics, compare screenshots, read verification reports, enforce checkpoint preconditions and return compact JSON.
- Unity UPM C# package: checks that require live Unity APIs and exports JSON files to .ai/artifacts/.
- Pi.dev Skills: orchestrate the sequence of Coplay MCP actions and CLI commands.

## Runtime
- Node.js >= 24.15.0; use built-in node:sqlite.
- Install CLI dependencies with npm --prefix packages/cli install.
- Run tests with npm --prefix packages/cli test.
- Unity baseline: 6000.3.23f1.

## Safety and evidence
- Never start another MCP server or add a C# socket/HTTP server.
- Never commit a Unity project checkpoint until Coplay MCP has invoked "Tools/AI Verification/Save Scene and Assets" and the CLI confirms a fresh successful save-state file.
- Prefer selected-object verification to scanning every object in the scene.
- Never return full Editor.log by default. Return grouped errors, counts and small source excerpts.
- Only read full stack traces through explicit diagnostic detail.
- Bound log tails, visual frame selection, image size and JSON output.

## Verification order
1. Apply changes through Coplay MCP.
2. Save scenes/assets using the Editor bridge menu item.
3. Run selected C# checks via Coplay MCP.
4. Run unity-ai verify and inspect bounded log/report evidence.
5. Use visual diff only when screenshots are relevant.
6. Create a checkpoint only after a fresh save confirmation.
7. Run tests and require green GitHub Actions.

## Done
Implementation + relevant Node/C# tests + updated Pi.dev Skills/docs + Git review + green CI.
