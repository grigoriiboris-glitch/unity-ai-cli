import unittest

from collector import UnityLogCollector


class UnityLogCollectorTests(unittest.TestCase):
    def test_collects_exception_block_without_forwarding_raw_log(self):
        log = """NullReferenceException: PlayerController failed
at UnityEngine.Debug.LogError () 
at PlayerController.Update () in Assets/PlayerController.cs:184

Some unrelated console line
"""
        blocks = UnityLogCollector().collect(
            log,
            task_id="TASK-20261007-001",
            run_id="RUN-001",
            raw_log_path=".ai/diagnostics/raw/editor.log",
        )

        self.assertEqual(len(blocks), 1)
        self.assertEqual(blocks[0].event.exception_type, "NullReferenceException")
        self.assertEqual(blocks[0].event.file, "Assets/PlayerController.cs")
        self.assertEqual(blocks[0].event.line, 184)
        self.assertEqual(blocks[0].event.task_id, "TASK-20261007-001")
        self.assertEqual(blocks[0].event.raw_log_path, ".ai/diagnostics/raw/editor.log")
        self.assertNotEqual(blocks[0].event.stacktrace, "")
        self.assertIn("PlayerController.Update", blocks[0].raw_text)
