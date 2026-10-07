# CI

## Mandatory

Every pull request must pass repository sanity.

The sanity job validates:
- required Unity project/package files;
- pinned Unity 6000.3.23f1;
- pinned Unity Test Framework 1.6.0;
- runtime package version 0.1.0;
- repository AI-memory files.

## Unity execution

The full Unity compile/EditMode/PlayMode job is intentionally separate from repository sanity because headless Unity licensing requires repository credentials.

Planned repository settings:
- `UNITY_CI_ENABLED=true`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`
- `UNITY_SERIAL`

The Unity job is added/enabled only after those credentials exist. The implementation uses pinned GameCI action commits rather than `latest`.
