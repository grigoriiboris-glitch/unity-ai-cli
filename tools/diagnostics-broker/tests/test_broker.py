import json
import tempfile
import unittest
from pathlib import Path

from broker import DiagnosticEvent, DiagnosticsBroker


class DiagnosticsBrokerTests(unittest.TestCase):
    def new_broker(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        return DiagnosticsBroker(Path(directory.name) / "diagnostics.sqlite3")

    def test_identical_exception_storm_is_deduplicated(self):
        broker = self.new_broker()
        task_id = broker.new_task_id()

        event = DiagnosticEvent(
            severity="error",
            category="exception",
            message="NullReferenceException: Player id 42 at 2026-10-07T12:00:00Z",
            exception_type="NullReferenceException",
            file="Assets/PlayerController.cs",
            line=184,
            top_user_frame="PlayerController.Update",
            stacktrace="at PlayerController.Update () [0x00000] in Assets/PlayerController.cs:184",
            task_id=task_id,
            run_id="RUN-001",
            git_commit="abc123",
            raw_log_path=".ai/diagnostics/raw/editor.log",
        )

        first_id = broker.ingest(event)
        for _ in range(9999):
            broker.ingest(event)

        summary = broker.diagnostics_summary(task_id=task_id, max_chars=3000)
        self.assertEqual(summary["count"], 1)
        self.assertEqual(summary["diagnostics"][0]["count"], 10000)
        self.assertEqual(summary["diagnostics"][0]["id"], first_id)

    def test_stacktrace_only_appears_in_detail(self):
        broker = self.new_broker()
        event = DiagnosticEvent(
            severity="error",
            category="exception",
            message="Boom",
            exception_type="Exception",
            stacktrace="at PlayerController.Update ()",
            task_id="TASK-20261007-001",
            run_id="RUN-1",
        )
        diagnostic_id = broker.ingest(event)

        summary = broker.diagnostics_summary()
        encoded = json.dumps(summary)
        self.assertNotIn("PlayerController.Update", encoded)

        detail = broker.diagnostics_detail(diagnostic_id)
        self.assertEqual(detail["status"], "ok")
        self.assertEqual(detail["diagnostic"]["stacktrace"], "at PlayerController.Update ()")

    def test_same_fingerprint_can_be_correlated_to_multiple_tasks(self):
        broker = self.new_broker()
        base = dict(
            severity="error",
            category="exception",
            message="Object reference not set to an instance of an object",
            exception_type="NullReferenceException",
            file="Assets/Game.cs",
            line=10,
            top_user_frame="Game.Update",
        )

        broker.ingest(DiagnosticEvent(**base, task_id="TASK-20261007-001", run_id="RUN-1"))
        broker.ingest(DiagnosticEvent(**base, task_id="TASK-20261007-002", run_id="RUN-2"))

        first = broker.diagnostics_since("TASK-20261007-001")
        second = broker.diagnostics_since("TASK-20261007-002")
        self.assertEqual(first["count"], 1)
        self.assertEqual(second["count"], 1)

        cleared = broker.diagnostics_clear("TASK-20261007-001")
        self.assertEqual(cleared["cleared_occurrences"], 1)
        self.assertEqual(broker.diagnostics_since("TASK-20261007-001")["count"], 0)
        self.assertEqual(broker.diagnostics_since("TASK-20261007-002")["count"], 1)

    def test_output_is_bounded(self):
        broker = self.new_broker()
        for index in range(50):
            broker.ingest(
                DiagnosticEvent(
                    severity="error",
                    category="exception",
                    message=f"Different failure {index}",
                    exception_type="Exception",
                )
            )

        summary = broker.diagnostics_summary(page_size=20, max_chars=500)
        self.assertLessEqual(len(json.dumps(summary, ensure_ascii=False, separators=(",", ":"))), 500)
        self.assertTrue(summary.get("truncated"))

    def test_task_ids_are_daily_sequenced(self):
        broker = self.new_broker()
        task1 = broker.new_task_id()
        task2 = broker.new_task_id()
        self.assertRegex(task1, r"^TASK-\\d{8}-\\d{3}$")
        self.assertRegex(task2, r"^TASK-\\d{8}-\\d{3}$")
        self.assertEqual(int(task2[-3:]), int(task1[-3:]) + 1)


if __name__ == "__main__":
    unittest.main()
