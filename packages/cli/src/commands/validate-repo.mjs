import { access, readFile, readdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../../../");
const required = [
  "README.md",
  "LICENSE",
  "CONTRIBUTING.md",
  "AGENTS.md",
  ".ai/CHECKPOINT.md",
  ".ai/NEXT.md",
  ".ai/DECISIONS.md",
  ".agents/skills/unity/SKILL.md",
  ".agents/skills/unity-self-check/SKILL.md",
  ".agents/skills/unity-fix-compile-errors/SKILL.md",
  ".agents/skills/unity-visual-qa/SKILL.md",
  ".github/workflows/ci.yml",
  "docs/ARCHITECTURE.md",
  "docs/CI.md",
  "docs/COPLAY-MCP.md",
  "Packages/manifest.json",
  "ProjectSettings/ProjectVersion.txt",
  "Packages/com.grigoriiboris-glitch.ai-verification/package.json",
  "Packages/com.grigoriiboris-glitch.ai-verification/Editor/UnityAI.Verification.Editor.asmdef",
  "Packages/com.grigoriiboris-glitch.ai-verification/Editor/VerificationReportExporter.cs",
  "Packages/com.grigoriiboris-glitch.ai-verification/Editor/VerificationSaveStateInvalidator.cs",
  "packages/cli/package.json",
  "packages/cli/src/index.mjs",
  "packages/cli/src/db.mjs",
  "packages/cli/src/logs.mjs",
  "packages/cli/src/visual-diff.mjs",
  "packages/cli/src/checkpoint.mjs",
  "packages/cli/src/verify.mjs",
  "packages/cli/src/commands/validate-repo.mjs",
  "packages/cli/test/db.test.mjs",
  "packages/cli/test/logs.test.mjs",
  "packages/cli/test/visual-diff.test.mjs",
  "packages/cli/test/checkpoint.test.mjs",
  "packages/cli/test/verify.test.mjs"
];

for (const relative of required) {
  try {
    await access(path.join(repoRoot, relative));
  } catch {
    throw new Error(`Repository contract: missing required file: ${relative}`);
  }
}

async function findPythonFiles(directory, relative = "") {
  const entries = await readdir(directory, { withFileTypes: true });
  const found = [];
  for (const entry of entries) {
    if ([".git", "node_modules", "Library", "Temp", "Obj"].includes(entry.name)) continue;
    const childRelative = path.join(relative, entry.name);
    const absolute = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      found.push(...await findPythonFiles(absolute, childRelative));
    } else if (entry.isFile() && entry.name.endsWith(".py")) {
      found.push(childRelative.replaceAll(path.sep, "/"));
    }
  }
  return found;
}

const pythonFiles = await findPythonFiles(repoRoot);
if (pythonFiles.length) {
  throw new Error(`Python tools remain after migration: ${pythonFiles.join(", ")}`);
}

const projectVersion = await readFile(path.join(repoRoot, "ProjectSettings/ProjectVersion.txt"), "utf8");
if (!projectVersion.includes("m_EditorVersion: 6000.3.23f1")) {
  throw new Error("Unity version is not pinned to 6000.3.23f1");
}
const manifest = JSON.parse(await readFile(path.join(repoRoot, "Packages/manifest.json"), "utf8"));
if (manifest.dependencies?.["com.unity.test-framework"] !== "1.6.0") {
  throw new Error("Unity Test Framework must be pinned to 1.6.0");
}
const unityPackage = JSON.parse(await readFile(
  path.join(repoRoot, "Packages/com.grigoriiboris-glitch.ai-verification/package.json"), "utf8"
));
if (unityPackage.version !== "0.1.0" || unityPackage.unity !== "6000.3") {
  throw new Error("Unity verification package version/compatibility changed unexpectedly");
}
const cliPackage = JSON.parse(await readFile(path.join(repoRoot, "packages/cli/package.json"), "utf8"));
if (cliPackage.engines?.node !== ">=24.15.0") {
  throw new Error("CLI must target Node.js >=24.15.0 (node:sqlite release-candidate API)");
}
console.log("Repository sanity: PASS");
