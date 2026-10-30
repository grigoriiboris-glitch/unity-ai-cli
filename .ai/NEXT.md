# Next

1. Run Node.js 24.15+ validation, syntax checks and all CLI regression tests.
2. Confirm the Editor assembly compiles in Unity 6000.3.23f1; fix API/version errors if any.
3. Exercise the Coplay menu invocation path in an open Unity Editor: save state -> selected-object report -> unity-ai verify.
4. Test checkpoint refusal for missing/stale/dirty save state and successful local tag creation after a valid save.
5. Keep Unity compile/EditMode/PlayMode CI opt-in until Unity license secrets are configured.
6. Review PR diff, confirm no Python files or duplicated MCP transport remain, then merge only with green GitHub Actions.
