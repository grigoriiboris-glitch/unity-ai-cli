# AGENTS.md — Unity AI Verification Runtime

## Mission
Build deterministic verification around Unity so Codex receives compact evidence instead of full project state or raw logs.

## Rules
- Never return the full Unity log automatically.
- Return IDs, summaries, counts, and bounded evidence.
- Keep persistent state in `.ai/` + Git.
- Prefer targeted file/symbol edits.
- Unity 6000.3.23f1 is the pinned baseline.
- Unity CLI is deterministic control; Coplay MCP is Editor interaction.
- Do not implement a second Unity MCP server.

## Verification order
1. compile/tests;
2. diagnostics summary;
3. runtime snapshot;
4. physics/bounds/visibility;
5. temporal checks when required;
6. visual evidence only when semantic checks cannot prove the result.

## Done
Implementation + relevant tests + checkpoint + Git review + green CI.