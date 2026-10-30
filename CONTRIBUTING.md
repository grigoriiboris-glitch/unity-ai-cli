# Contributing

Every logical slice: implement -> test -> review diff -> update checkpoint -> commit -> green CI -> merge.

Unity baseline: 6000.3.23f1. Do not silently change it in a feature PR.

Runtime tooling: Node.js >=24.15.0. Run npm --prefix packages/cli install, npm --prefix packages/cli run lint and npm --prefix packages/cli test.

Architecture rule: Coplay MCP is the sole Unity Editor action transport. Do not add a second MCP server, C# socket or HTTP listener. Keep live-engine checks in the C# UPM package; keep filesystem/log/image/Git analysis in Node.js.

Before creating a Git checkpoint, invoke Tools/AI Verification/Save Scene and Assets through Coplay MCP and ensure the CLI accepts the fresh save-state report.
