import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SELECT = ROOT / "tools" / "visual-evidence" / "select_frames.py"


class FrameSelectionTests(unittest.TestCase):
    def run_select(self, directory, *args):
        output = subprocess.check_output(
            [sys.executable, str(SELECT), str(directory), *args],
            text=True,
        )
        return json.loads(output)

    def make(self, directory, index, value):
        Image.new("RGB", (8, 8), (value, value, value)).save(
            directory / f"frame-{index:03d}.png"
        )

    def test_small_sequence_is_not_reduced(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            self.make(path, 0, 0)
            self.make(path, 1, 10)
            self.make(path, 2, 20)

            result = self.run_select(path, "--max-frames", "5")

            self.assertEqual(result["total_frames"], 3)
            self.assertEqual(result["selected_frames"], [
                "frame-000.png", "frame-001.png", "frame-002.png"
            ])

    def test_large_sequence_is_bounded_and_keeps_edges(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            for i in range(20):
                self.make(path, i, 10 if i != 12 else 200)

            result = self.run_select(path, "--max-frames", "4", "--threshold", "20")

            self.assertEqual(result["status"], "ok")
            self.assertEqual(len(result["selected_frames"]), 4)
            self.assertIn("frame-000.png", result["selected_frames"])
            self.assertIn("frame-019.png", result["selected_frames"])

    def test_empty_sequence_returns_empty(self):
        with tempfile.TemporaryDirectory() as directory:
            result = self.run_select(Path(directory))
            self.assertEqual(result["selected_frames"], [])


if __name__ == "__main__":
    unittest.main()
