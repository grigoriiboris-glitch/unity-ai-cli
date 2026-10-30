---
name: unity-visual-qa
description: Verify Unity Game View screenshots with bounded frame selection and perceptual pixel diffs after scene or UI changes. Use when visual layout, rendering or animation needs objective comparison.
---

# Unity visual QA

1. Make the requested changes through Coplay MCP.
2. Save the scene and assets using the Unity save/checkpoint workflow before creating a baseline or checkpoint.
3. Use existing Coplay MCP camera, Game View and Editor tools to capture the relevant screenshot or bounded frame sequence. Do not add a screenshot transport to this CLI.
4. Compare one baseline/current pair:
      node packages/cli/src/index.mjs visual-diff baseline.png current.png --out .ai/artifacts/visual-diff.png
5. For a sequence, select representative frames:
      node packages/cli/src/index.mjs visual-select ./frames --max-frames 5 --threshold 0.25
6. Inspect the generated diff image and result JSON. The result includes diff percentage, changed pixel count, connected changed regions, bounding box and severity.
7. Treat error severity as a failed visual check. Investigate warning severity against the task acceptance criteria.
8. If the diff is caused by expected content, update the baseline deliberately and describe why. Never silently accept a changed baseline.
9. Re-run unity-self-check and save the final scene before creating the final checkpoint.

## Limits
- Frame selection is capped at 100 frames.
- Image inputs are capped at 32 million pixels.
- Do not capture or compare every frame by default; select only evidence needed for the task.
