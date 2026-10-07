#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from broker import DiagnosticsBroker, event_from_mapping, load_json_lines


def _json(value: object) -> None:
    print(json.dumps(value, ensure_ascii=False, separators=(",", ":")))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Unity AI diagnostics broker")
    parser.add_argument(
        "--db",
        default=".ai/diagnostics/diagnostics.sqlite3",
        help="SQLite database path",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("task")

    ingest = sub.add_parser("ingest")
    ingest.add_argument("--event", help="JSON event")
    ingest.add_argument("--jsonl", help="JSONL file containing events")

    summary = sub.add_parser("summary")
    summary.add_argument("--task-id", default="")
    summary.add_argument("--page", type=int, default=1)
    summary.add_argument("--page-size", type=int, default=20)
    summary.add_argument("--max-chars", type=int, default=3000)

    search = sub.add_parser("search")
    search.add_argument("query")
    search.add_argument("--page", type=int, default=1)
    search.add_argument("--page-size", type=int, default=20)
    search.add_argument("--max-chars", type=int, default=3000)

    detail = sub.add_parser("detail")
    detail.add_argument("diagnostic_id")

    since = sub.add_parser("since")
    since.add_argument("task_id")
    since.add_argument("--max-chars", type=int, default=3000)

    clear = sub.add_parser("clear")
    clear.add_argument("task_id")

    return parser


def main() -> int:
    args = build_parser().parse_args()
    broker = DiagnosticsBroker(Path(args.db))

    if args.command == "task":
        _json({"status": "ok", "task_id": broker.new_task_id()})
        return 0

    if args.command == "ingest":
        if bool(args.event) == bool(args.jsonl):
            raise SystemExit("use exactly one of --event or --jsonl")

        count = 0
        if args.event:
            diagnostic_id = broker.ingest(event_from_mapping(json.loads(args.event)))
            _json({"status": "ok", "diagnostic_id": diagnostic_id, "ingested": 1})
            return 0

        for event in load_json_lines(args.jsonl):
            broker.ingest(event)
            count += 1

        _json({"status": "ok", "ingested": count})
        return 0

    if args.command == "summary":
        _json(
            broker.diagnostics_summary(
                task_id=args.task_id,
                page=args.page,
                page_size=args.page_size,
                max_chars=args.max_chars,
            )
        )
        return 0

    if args.command == "search":
        _json(
            broker.diagnostics_search(
                args.query,
                page=args.page,
                page_size=args.page_size,
                max_chars=args.max_chars,
            )
        )
        return 0

    if args.command == "detail":
        _json(broker.diagnostics_detail(args.diagnostic_id))
        return 0

    if args.command == "since":
        _json(broker.diagnostics_since(args.task_id, max_chars=args.max_chars))
        return 0

    if args.command == "clear":
        _json(broker.diagnostics_clear(args.task_id))
        return 0

    return 1


if __name__ == "__main__":
    sys.exit(main())
