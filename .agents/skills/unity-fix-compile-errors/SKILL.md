---
name: unity-fix-compile-errors
description: Diagnose and repair Unity C# compilation and repeated runtime exceptions using bounded Editor.log summaries and source excerpts. Use when Unity compilation or Play Mode errors occur.
---

# Unity compile and exception repair

1. Confirm the Unity project root and the configured Editor.log path.
2. Run:
      node packages/cli/src/index.mjs logs summarize --include-source
   Or pass the exact path:
      node packages/cli/src/index.mjs logs summarize /path/to/Editor.log --include-source
3. Start with the highest-count error groups. The output includes normalized message, source file, line, top user frame, occurrence count and at most five source lines on each side when the path is inside the repository.
4. Read the full stack trace only for one selected diagnostic when the short excerpt is insufficient. Do not load the full Editor.log.
5. Fix the root cause using the existing Coplay MCP script editing/validation tools or a minimal source patch.
6. Ask Coplay MCP to refresh/compile scripts. Use read_console for the current compiler result.
7. Rerun the bounded log summary and targeted Unity tests. A compiler error is not resolved until it disappears from the fresh log/Console result.
8. Run the unity-self-check workflow before reporting completion.

## Guardrails
- Do not suppress exceptions or lower log levels merely to make the report green.
- Do not retry indefinitely. After one failed repair, inspect the new stack/source context and revise the diagnosis.
- Keep Unity MCP as the sole Editor action transport.
