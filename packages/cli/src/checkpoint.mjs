import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { execFileSync } from "node:child_process";

function git(args, cwd) {
  return execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

function slugify(value) {
  const slug = String(value || "checkpoint")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "")
    .slice(0, 48);
  return slug || "checkpoint";
}

export async function readEditorSaveState(repositoryRoot, statePath = ".ai/artifacts/editor-save-state.json") {
  const absolute = path.resolve(repositoryRoot, statePath);
  let state;
  try {
    state = JSON.parse(await readFile(absolute, "utf8"));
  } catch (error) {
    throw new Error(
      `Unity save confirmation is missing or unreadable at ${statePath}. Run Coplay MCP menu item "Tools/AI Verification/Save Scene and Assets" first.`,
      { cause: error }
    );
  }
  if (state.status !== "saved" || state.scenesSaved !== true || state.assetsSaved !== true || state.prefabStageSaved === false || Number(state.unsavedSceneCount ?? 0) > 0) {
    throw new Error("Checkpoint refused: Unity reports unsaved scenes or assets. Save through the existing Unity MCP and retry.");
  }
  const savedAt = Date.parse(state.savedAt ?? "");
  const maxAgeMs = 10 * 60 * 1000;
  if (!Number.isFinite(savedAt) || Date.now() - savedAt > maxAgeMs || savedAt > Date.now() + 60_000) {
    throw new Error("Checkpoint refused: Unity save confirmation is older than 10 minutes or has an invalid timestamp. Save the scene again through Unity MCP.");
  }
  return state;
}

export async function createCheckpoint(repositoryRoot, name, {
  statePath = ".ai/artifacts/editor-save-state.json",
  commitMessage = null
} = {}) {
  const root = path.resolve(repositoryRoot);
  const state = await readEditorSaveState(root, statePath);
  const gitRoot = git(["rev-parse", "--show-toplevel"], root);
  if (path.resolve(gitRoot) !== root) throw new Error("Checkpoint must run from the Unity repository root.");
  const timestamp = new Date().toISOString().replaceAll(":", "").replaceAll("-", "").replace(/\.\d{3}Z$/, "Z");
  const slug = slugify(name);
  const tag = `ai-checkpoint/${timestamp}-${slug}`;
  const currentBranch = git(["branch", "--show-current"], root);
  if (!currentBranch) throw new Error("Checkpoint refused: detached HEAD.");
  git(["add", "-A"], root);
  const hasChanges = Boolean(git(["status", "--porcelain"], root));
  let commitCreated = false;
  if (hasChanges) {
    const message = commitMessage || `checkpoint: ${name}`;
    git(["commit", "-m", message], root);
    commitCreated = true;
  }
  const commit = git(["rev-parse", "HEAD"], root);
  git(["tag", "-a", tag, "-m", `Unity AI checkpoint: ${name}`], root);

  const checkpointDirectory = path.join(root, ".ai", "checkpoints");
  await mkdir(checkpointDirectory, { recursive: true });
  const record = {
    name,
    tag,
    commit,
    branch: currentBranch,
    commitCreated,
    createdAt: new Date().toISOString(),
    savedAt: state.savedAt,
    scenes: state.scenes ?? []
  };
  const recordPath = path.join(checkpointDirectory, `${timestamp}-${slug}.json`);
  await writeFile(recordPath, `${JSON.stringify(record, null, 2)}\n`, "utf8");
  return { status: "ok", ...record, record: path.relative(root, recordPath).replaceAll(path.sep, "/") };
}

export function listCheckpoints(repositoryRoot) {
  const output = git(["tag", "--list", "ai-checkpoint/*", "--sort=-creatordate"], path.resolve(repositoryRoot));
  return { status: "ok", checkpoints: output ? output.split("\n") : [] };
}
