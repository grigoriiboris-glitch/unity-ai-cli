# Checkpoint

- current task: Node.js CLI + Coplay MCP verification bridge
- status: implementation-in-progress
- branch: refactor/node-cli-coplay-verifier-20261030
- base: main (Unity 6000.3.23f1)
- architecture: Coplay MCP owns all Editor actions; Node.js CLI owns filesystem diagnostics, visual diff and Git guardrails; C# UPM package exports live-scene verification JSON.
- completed: Node.js diagnostics broker, Editor.log deduplication/source snippets, sharp + pixelmatch diff/frame selection, checkpoint save-state gate, Coplay Editor menu bridge, Pi.dev skill drafts, Node CI workflow.
- pending verification: install Node dependencies, run regression tests and CI; review Unity Editor assembly compilation inside Unity 6000.3.23f1.
- Unity Editor bridge: Tools/AI Verification/Save Scene and Assets; Tools/AI Verification/Run Selected Checks.
- file contracts: .ai/artifacts/editor-save-state.json; .ai/artifacts/verification-report.json.
- do not merge until Node CI is green and the C# editor assembly is reviewed.
