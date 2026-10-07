# Unity verification skill

Default loop:
`inspect -> patch -> compile -> diagnostics summary -> runtime verification -> visual evidence if needed -> test -> checkpoint -> commit`

Never request the full Unity Console by default. Prefer deterministic RuntimeVerifier results before vision analysis.

Diagnostics escalation: `summary -> ID -> detail -> source -> patch -> re-run`.

Visual escalation: `runtime -> anomaly -> Game View -> Scene View -> selected evidence -> patch -> re-run`.

Do not declare success until relevant verification is green and GitHub Actions is green.
