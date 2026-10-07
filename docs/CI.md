# CI

## Mandatory

Every pull request must pass repository sanity and diagnostics broker tests.

## Unity execution

The Unity job is always present in the workflow but is opt-in:

`UNITY_CI_ENABLED=true`

Repository secrets required when enabled:
- `UNITY_EMAIL`
- `UNITY_PASSWORD`
- `UNITY_SERIAL`

The job runs Unity 6000.3.23f1 and all EditMode/PlayMode tests through pinned `game-ci/unity-test-runner` v4.4.0.

Without the variable enabled, the Unity job is skipped while repository sanity and Python tests remain mandatory.
