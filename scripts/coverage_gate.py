#!/usr/bin/env python3
"""Enforce Cobertura coverage thresholds for CI.

The CI summary displays coverage values rounded to two decimal places. This gate
uses the same two-decimal rounded values for comparisons so a displayed 80.00%
passes an 80.00% threshold even if the raw value is 79.995%.
"""

from __future__ import annotations

import os
import pathlib
import sys
import xml.etree.ElementTree as ET
from decimal import Decimal, ROUND_HALF_UP
from typing import TextIO

_TWO_DECIMAL_PLACES = Decimal("0.01")


def rounded_percent(value: Decimal | float | str) -> Decimal:
    """Round a percent value to two decimal places for display/comparison."""
    return Decimal(str(value)).quantize(_TWO_DECIMAL_PLACES, rounding=ROUND_HALF_UP)


def rate_to_percent(rate: str) -> Decimal:
    """Convert a Cobertura rate string such as '0.79995' to rounded percent."""
    return rounded_percent(Decimal(rate) * Decimal("100"))


def evaluate_coverage(
    coverage_file: pathlib.Path,
    line_threshold: Decimal | float | str,
    branch_threshold: Decimal | float | str,
    stdout: TextIO = sys.stdout,
    stderr: TextIO = sys.stderr,
) -> int:
    """Return 0 when rounded coverage satisfies rounded thresholds; otherwise 1."""
    rounded_line_threshold = rounded_percent(line_threshold)
    rounded_branch_threshold = rounded_percent(branch_threshold)

    if not coverage_file.exists():
        print(f"Coverage file not found: {coverage_file}", file=stderr)
        return 1

    root = ET.parse(coverage_file).getroot()
    line_rate = rate_to_percent(root.attrib.get("line-rate", "0"))
    branch_rate = rate_to_percent(root.attrib.get("branch-rate", "0"))

    print(f"Line coverage: {line_rate:.2f}%", file=stdout)
    print(f"Branch coverage: {branch_rate:.2f}%", file=stdout)
    print(f"Required line coverage: {rounded_line_threshold:.2f}%", file=stdout)
    print(f"Required branch coverage: {rounded_branch_threshold:.2f}%", file=stdout)

    failures = []
    if line_rate < rounded_line_threshold:
        failures.append(
            f"Line coverage {line_rate:.2f}% is below the required {rounded_line_threshold:.2f}% threshold."
        )
    if branch_rate < rounded_branch_threshold:
        failures.append(
            f"Branch coverage {branch_rate:.2f}% is below the required {rounded_branch_threshold:.2f}% threshold."
        )

    if failures:
        for failure in failures:
            print(failure, file=stderr)
        return 1

    return 0


def main() -> int:
    coverage_file = pathlib.Path(os.environ["COVERAGE_REPORT_DIR"]) / "Cobertura.xml"
    line_threshold = os.environ["COVERAGE_LINE_THRESHOLD"]
    branch_threshold = os.environ["COVERAGE_BRANCH_THRESHOLD"]
    return evaluate_coverage(coverage_file, line_threshold, branch_threshold)


if __name__ == "__main__":
    sys.exit(main())
