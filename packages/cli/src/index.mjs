#!/usr/bin/env node
import path from "node:path";
import { fileURLToPath } from "node:url";
import { DiagnosticsBroker, eventFromMapping, loadJsonLines } from "./db.mjs";
import { summarizeEditorLog, defaultEditorLogPath } from "./logs.mjs";
import { compareImages, selectFrames } from "./visual-diff.mjs";
import { createCheckpoint, listCheckpoints } from "./checkpoint.mjs";
import { verifyProject } from "./verify.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");

function parseArgs(tokens) {
  const positional = [];
  const options = {};
  for (let i = 0; i < tokens.length; i += 1) {
    const token = tokens[i];
    if (!token.startsWith("--")) {
      positional.push(token);
      continue;
    }
    const key = token.slice(2);
    if (["include-source", "json", "help"].includes(key)) {
      options[key] = true;
    } else if (token.includes("=")) {
      const [name, ...rest] = key.split("=");
      options[name] = rest.join("=");
    } else {
      options[key] = tokens[i + 1] && !tokens[i + 1].startsWith("--") ? tokens[++i] : true;
    }
  }
  return { positional, options };
}

function printJson(value) {
  process.stdout.write(`${JSON.stringify(value, null, 2)}\n`);
}

function usage() {
  process.stdout.write(`unity-ai — Unity verification and guardrails CLI

Usage:
  unity-ai validate
  unity-ai task
  unity-ai diagnostics task
  unity-ai diagnostics ingest --event '<json>'
  unity-ai diagnostics ingest --jsonl <file>
  unity-ai diagnostics summary [--task-id <id>] [--page 1] [--page-size 20]
  unity-ai diagnostics search <query>
  unity-ai diagnostics detail <diagnostic-id>
  unity-ai diagnostics since <task-id>
  unity-ai diagnostics clear <task-id>
  unity-ai logs summarize [Editor.log] [--include-source]
  unity-ai visual-diff <baseline> <current> [--out <diff.png>] [--threshold 0.06]
  unity-ai visual-select <frames-directory> [--max-frames 5] [--threshold 0.25]
  unity-ai verify [--suite logs,physics,ui] [--editor-log <path>]
  unity-ai checkpoint create <name>
  unity-ai checkpoint list

Unity Editor actions are performed by the configured Unity MCP (Coplay MCP by default).
The CLI does not host an MCP server or open a second connection to the Editor.
`);
}

async function main(argv) {
  const [command, ...rest] = argv;
  if (!command || command === "--help" || command === "help") return usage();
  const { positional, options } = parseArgs(rest);
  const dbPath = options.db || ".ai/diagnostics/diagnostics.sqlite3";

  if (command === "validate") {
    const { spawnSync } = await import("node:child_process");
    const script = path.join(path.dirname(fileURLToPath(import.meta.url)), "commands/validate-repo.mjs");
    const result = spawnSync(process.execPath, [script], { cwd: repoRoot, encoding: "utf8" });
    if (result.stdout) process.stdout.write(result.stdout);
    if (result.stderr) process.stderr.write(result.stderr);
    if (result.status !== 0) process.exitCode = result.status || 1;
    return;
  }

  if (command === "task" || (command === "diagnostics" && positional[0] === "task")) {
    const broker = new DiagnosticsBroker(dbPath);
    try {
      printJson({ status: "ok", task_id: broker.newTaskId() });
    } finally {
      broker.close();
    }
    return;
  }

  if (command === "diagnostics") {
    const [subcommand, ...args] = positional;
    const broker = new DiagnosticsBroker(dbPath);
    try {
      if (subcommand === "ingest") {
        if (options.event && options.jsonl) throw new Error("Use either --event or --jsonl, not both.");
        if (typeof options.event === "string") {
          const event = eventFromMapping(JSON.parse(options.event));
          printJson({ status: "ok", diagnostic_id: broker.ingest(event), ingested: 1 });
          return;
        }
        if (typeof options.jsonl === "string") {
          const events = await loadJsonLines(options.jsonl);
          for (const event of events) broker.ingest(event);
          printJson({ status: "ok", ingested: events.length });
          return;
        }
        throw new Error("diagnostics ingest requires --event '<json>' or --jsonl <file>.");
      }
      if (subcommand === "summary") {
        printJson(broker.diagnosticsSummary({
          taskId: options["task-id"] || "",
          page: options.page || 1,
          pageSize: options["page-size"] || 20,
          maxChars: options["max-chars"] || 3000
        }));
        return;
      }
      if (subcommand === "search") {
        if (!args.length) throw new Error("diagnostics search requires a query.");
        printJson(broker.diagnosticsSearch(args.join(" "), {
          page: options.page || 1,
          pageSize: options["page-size"] || 20,
          maxChars: options["max-chars"] || 3000
        }));
        return;
      }
      if (subcommand === "detail") {
        if (!args[0]) throw new Error("diagnostics detail requires an ID.");
        printJson(broker.diagnosticsDetail(args[0]));
        return;
      }
      if (subcommand === "since") {
        if (!args[0]) throw new Error("diagnostics since requires a task ID.");
        printJson(broker.diagnosticsSince(args[0], { maxChars: options["max-chars"] || 3000 }));
        return;
      }
      if (subcommand === "clear") {
        if (!args[0]) throw new Error("diagnostics clear requires a task ID.");
        printJson(broker.diagnosticsClear(args[0]));
        return;
      }
      throw new Error(`Unknown diagnostics command: ${subcommand || "(missing)"}`);
    } finally {
      broker.close();
    }
  }

  if (command === "logs") {
    if (positional[0] !== "summarize") throw new Error("Usage: unity-ai logs summarize [Editor.log]");
    const logPath = positional[1] || options.file || defaultEditorLogPath();
    printJson(await summarizeEditorLog(logPath, {
      repositoryRoot: options.root || process.cwd(),
      includeSource: Boolean(options["include-source"]),
      maxLines: options["max-lines"] || 5000,
      maxItems: options["max-items"] || 40
    }));
    return;
  }

  if (command === "visual-diff") {
    if (positional.length < 2) throw new Error("Usage: unity-ai visual-diff <baseline> <current>");
    const result = await compareImages(positional[0], positional[1], {
      threshold: options.threshold ?? 0.06,
      outputPath: options.out || null
    });
    printJson(result);
    if (result.status !== "ok") process.exitCode = 1;
    return;
  }

  if (command === "visual-select") {
    if (!positional[0]) throw new Error("Usage: unity-ai visual-select <frames-directory>");
    const result = await selectFrames(positional[0], {
      maxFrames: options["max-frames"] || 5,
      threshold: options.threshold ?? 0.25
    });
    printJson(result);
    if (result.status !== "ok") process.exitCode = 1;
    return;
  }

  if (command === "verify") {
    const result = await verifyProject(repoRoot, {
      suites: String(options.suite || "logs,physics,ui").split(","),
      editorLog: options["editor-log"] || defaultEditorLogPath(),
      reportPath: options.report || ".ai/artifacts/verification-report.json",
      includeSource: options["include-source"] !== false,
      maxAgeMinutes: options["max-age-minutes"] || 15
    });
    printJson(result);
    if (result.status !== "passed") process.exitCode = 1;
    return;
  }

  if (command === "checkpoint") {
    const [subcommand, ...args] = positional;
    if (subcommand === "create") {
      if (!args.length) throw new Error("Usage: unity-ai checkpoint create <name>");
      printJson(await createCheckpoint(repoRoot, args.join(" "), {
        statePath: options["state-file"] || ".ai/artifacts/editor-save-state.json",
        commitMessage: options.message || null
      }));
      return;
    }
    if (subcommand === "list") {
      printJson(listCheckpoints(repoRoot));
      return;
    }
    throw new Error("Usage: unity-ai checkpoint create <name> | checkpoint list");
  }

  usage();
  throw new Error(`Unknown command: ${command}`);
}

main(process.argv.slice(2)).catch((error) => {
  process.stderr.write(`unity-ai: ${error.message}\n`);
  if (process.env.UNITY_AI_DEBUG === "1" && error.stack) process.stderr.write(`${error.stack}\n`);
  process.exitCode = 1;
});
