import { readFile, stat } from "node:fs/promises";
import path from "node:path";
import { defaultEditorLogPath, summarizeEditorLog } from "./logs.mjs";

const REQUIRED_REPORT_SUITES = new Set(["physics", "ui", "visibility", "bounds"]);

async function readReport(repositoryRoot, reportPath) {
  const fullPath = path.resolve(repositoryRoot, reportPath);
  try {
    return JSON.parse(await readFile(fullPath, "utf8"));
  } catch (error) {
    throw new Error(
      `Unity verification report is missing at ${reportPath}. Use the existing Unity MCP to run "Tools/AI Verification/Run Selected Checks" first.`,
      { cause: error }
    );
  }
}

export async function verifyProject(repositoryRoot, {
  suites = ["logs", "physics", "ui"],
  editorLog = defaultEditorLogPath(),
  reportPath = ".ai/artifacts/verification-report.json",
  includeSource = true,
  maxAgeMinutes = 15
} = {}) {
  const root = path.resolve(repositoryRoot);
  const normalizedSuites = [...new Set(suites.map((suite) => String(suite).trim().toLowerCase()).filter(Boolean))];
  const result = { status: "passed", repository: root, suites: normalizedSuites, checks: {} };

  if (normalizedSuites.includes("logs")) {
    const log = await summarizeEditorLog(editorLog, { repositoryRoot: root, includeSource });
    result.checks.logs = log;
    if (log.total_occurrences > 0) result.status = "failed";
  }

  const requestedUnitySuites = normalizedSuites.filter((suite) => REQUIRED_REPORT_SUITES.has(suite));
  if (requestedUnitySuites.length) {
    const report = await readReport(root, reportPath);
    const generatedAt = Date.parse(report.generatedAt ?? report.generated_at ?? "");
    const maxAgeMs = Math.max(1, Number(maxAgeMinutes)) * 60_000;
    if (!Number.isFinite(generatedAt) || Date.now() - generatedAt > maxAgeMs || generatedAt > Date.now() + 60_000) {
      throw new Error(`Unity verification report is stale (limit ${maxAgeMinutes} minutes). Run the verification menu item again through Unity MCP.`);
    }
    const suiteMap = report.suites ?? {};
    const missingSuites = requestedUnitySuites.filter((suite) => suiteMap[suite] === undefined && report.checkedSuites?.includes?.(suite) !== true);
    const anomalies = report.anomalies ?? report.Anomalies ?? [];
    const errors = anomalies.filter((item) => String(item.severity ?? "error").toLowerCase() === "error");
    result.checks.unity = {
      status: report.status,
      generatedAt: report.generatedAt ?? report.generated_at,
      checkedSuites: report.checkedSuites ?? Object.keys(suiteMap),
      missingSuites,
      anomalyCount: anomalies.length,
      errors,
      skipped: report.skipped ?? []
    };
    if (report.status !== "passed" || missingSuites.length || errors.length) result.status = "failed";
  }

  return result;
}
