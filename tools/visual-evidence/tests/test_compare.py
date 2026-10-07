import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
COMPARE = ROOT / "tools" / "visual-evidence" / "compare.py"


class CompareTests(unittest.TestCase):
    def make_image(self, path, value):
        image = Image.new("RGB", (10, 10), (value, value, value))
        image.save(path)

    def run_compare(self, baseline, current):
        output = subprocess.check_output(
            [sys.executable, str(COMPARE), str(baseline), str(current)],
            text=True,
        )
        return json.loads(output)

    def test_identical_images_have_zero_diff(self):
        with tempfile.TemporaryDirectory() as directory:
            baseline = Path(directory) / "baseline.png"
            current = Path(directory) / "current.png"
            self.make_image(baseline, 10)
            self.make_image(current, 10)

            result = self.run_compare(baseline, current)

            self.assertEqual(result["status"], "ok")
            self.assertEqual(result["diff_percent"], 0.0)
            self.assertEqual(result["changed_regions"], 0)

    def test_changed_image_reports_diff(self):
        with tempfile.TemporaryDirectory() as directory:
            baseline = Path(directory) / "baseline.png"
            current = Path(directory) / "current.png"
            self.make_image(baseline, 10)
            self.make_image(current, 100)

            result = self.run_compare(baseline, current)

            self.assertGreater(result["diff_percent"], 0)
            self.assertEqual(result["changed_regions"], 1)
            self.assertEqual(result["severity"], "error")

    def test_size_mismatch_is_bounded_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            baseline = Path(directory) / "baseline.png"
            current = Path(directory) / "current.png"
            self.make_image(baseline, 10)
            Image.new("RGB", (8, 8), (10, 10, 10)).save(current)

            result = self.run_compare(baseline, current)

            self.assertEqual(result["status"], "failed")
            self.assertIn("image_size_mismatch", result["error"])


if __name__ == "__main__":
    unittest.main()
