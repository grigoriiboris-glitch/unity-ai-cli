# Architecture

```text
Codex CLI
   |
   +-- filesystem / Git
   +-- Unity CLI
   +-- Coplay MCP
   |
Unity Editor
   |
   +-- RuntimeVerifier
   |     +-- RuntimeSnapshot
   |     +-- PhysicsVerifier
   |     +-- BoundsVerifier
   |     +-- VisibilityVerifier
   |     +-- TemporalMonitor
   |
   +-- future Visual Evidence Layer
   +-- future Diagnostics Broker
```

RuntimeVerifier owns objective runtime facts and must not know about Codex prompts, MCP transport, SQLite, or Git.

Default context policy: tool result <= 4 KB target, diagnostics summary <= 3 KB, collections paginated, stack traces on demand, screenshots selected only.
