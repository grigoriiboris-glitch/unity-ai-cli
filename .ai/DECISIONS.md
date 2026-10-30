# Decisions

## 2026-10-07 — Unity 6.3 LTS baseline

Decision: pin the project to Unity 6000.3.23f1.
Reason: Unity 6.3 is the LTS line and 6000.3.23f1 is a released 6.3 patch.
Impact: CI/local verification uses the pinned editor until an explicit upgrade slice is approved.

## 2026-10-30 — Action Layer and Verification Layer

Decision: keep Coplay MCP as the only Unity Editor action transport. Do not build a second MCP server, socket or HTTP listener.
Decision: use a Node.js >=24.15 CLI for filesystem diagnostics, log deduplication, screenshot diff and Git guardrails. Use the built-in node:sqlite API for the local diagnostics index.
Decision: keep Unity-dependent checks in the C# UPM package. Expose them through Unity Editor menu items and JSON files in .ai/artifacts/.
Decision: require a fresh successful C# save-state report before the CLI creates a Git checkpoint.
Reason: scene and prefab state lives inside Unity Editor; file-based reports reuse Coplay MCP's existing channel without another transport.
