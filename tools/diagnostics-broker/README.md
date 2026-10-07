# Diagnostics Broker

Local Python 3.10+ service/library for compact Unity diagnostics.

## Storage

SQLite is the index. Raw Unity logs stay on disk and are referenced by `raw_log_path`; summary responses never include raw log content.

## AI-facing operations

- `task` creates `TASK-YYYYMMDD-NNN`.
- `summary` returns compact deduplicated diagnostics.
- `search` returns bounded diagnostic search results.
- `detail` performs explicit deep inspection including stack trace.
- `since` returns diagnostics correlated to a task.
- `clear` removes task occurrence links without deleting global fingerprint history.
- `ingest` accepts structured Unity diagnostic JSON/JSONL.

Default response limits are 20 diagnostics and 3 KB.

## Fingerprint

The fingerprint uses exception type, normalized message, source file/line, and the top user-code frame.

Repeated identical events increment `count` instead of creating another diagnostic.

Example event fields: `severity`, `category`, `exception_type`, `message`, `file`, `line`, `top_user_frame`, `stacktrace`, `task_id`, `run_id`, `git_commit`, `raw_log_path`.
