# Next

1. Configure UNITY_CI_ENABLED=true and Unity credentials so compile/EditMode/PlayMode execute in GitHub Actions.
2. Implement Unity Editor Game View / Scene View capture adapter using the existing bounded Visual Evidence API.
3. Persist evidence artifacts under .ai/artifacts/<task_id>/ and return artifact IDs only to AI-facing APIs.
4. Add visual escalation policy: semantic/runtime -> one screenshot -> Game+Scene -> selected temporal frames.
5. Add Codex/MCP integration and context recovery from CHECKPOINT + Git.
