import io
import pathlib
import tempfile
import unittest

from scripts.coverage_gate import evaluate_coverage


class CoverageGateTests(unittest.TestCase):
    def test_rounded_displayed_threshold_values_pass(self):
        coverage_file = self.write_coverage(line_rate="0.79995", branch_rate="0.7037")
        stdout = io.StringIO()
        stderr = io.StringIO()

        exit_code = evaluate_coverage(coverage_file, "80", "70", stdout=stdout, stderr=stderr)

        self.assertEqual(0, exit_code)
        self.assertIn("Line coverage: 80.00%", stdout.getvalue())
        self.assertIn("Branch coverage: 70.37%", stdout.getvalue())
        self.assertIn("Required line coverage: 80.00%", stdout.getvalue())
        self.assertEqual("", stderr.getvalue())

    def test_displayed_value_below_threshold_fails(self):
        coverage_file = self.write_coverage(line_rate="0.79994", branch_rate="0.7000")
        stdout = io.StringIO()
        stderr = io.StringIO()

        exit_code = evaluate_coverage(coverage_file, "80", "70", stdout=stdout, stderr=stderr)

        self.assertEqual(1, exit_code)
        self.assertIn("Line coverage: 79.99%", stdout.getvalue())
        self.assertIn("Line coverage 79.99% is below the required 80.00% threshold.", stderr.getvalue())

    def test_rounded_threshold_values_are_used_for_branch_comparison(self):
        coverage_file = self.write_coverage(line_rate="0.9000", branch_rate="0.7000")
        stdout = io.StringIO()
        stderr = io.StringIO()

        exit_code = evaluate_coverage(coverage_file, "79.995", "69.995", stdout=stdout, stderr=stderr)

        self.assertEqual(0, exit_code)
        self.assertIn("Required line coverage: 80.00%", stdout.getvalue())
        self.assertIn("Required branch coverage: 70.00%", stdout.getvalue())
        self.assertEqual("", stderr.getvalue())

    def test_branch_gate_still_fails_when_displayed_value_is_below_threshold(self):
        coverage_file = self.write_coverage(line_rate="0.8000", branch_rate="0.69994")
        stdout = io.StringIO()
        stderr = io.StringIO()

        exit_code = evaluate_coverage(coverage_file, "80", "70", stdout=stdout, stderr=stderr)

        self.assertEqual(1, exit_code)
        self.assertIn("Branch coverage: 69.99%", stdout.getvalue())
        self.assertIn("Branch coverage 69.99% is below the required 70.00% threshold.", stderr.getvalue())

    @staticmethod
    def write_coverage(line_rate: str, branch_rate: str) -> pathlib.Path:
        handle = tempfile.NamedTemporaryFile("w", suffix=".xml", delete=False)
        with handle:
            handle.write(f'<coverage line-rate="{line_rate}" branch-rate="{branch_rate}" />')
        return pathlib.Path(handle.name)


if __name__ == "__main__":
    unittest.main()
