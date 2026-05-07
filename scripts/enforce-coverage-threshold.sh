#!/usr/bin/env bash
set -euo pipefail

coverage_file="${1:-coverage/Cobertura.xml}"
threshold="${2:-80}"

if [[ ! -f "$coverage_file" ]]; then
  echo "Coverage file not found: $coverage_file" >&2
  exit 1
fi

python3 - "$coverage_file" "$threshold" <<'PY'
import sys
import xml.etree.ElementTree as ET

coverage_file = sys.argv[1]
threshold = float(sys.argv[2])
root = ET.parse(coverage_file).getroot()
line_rate = root.attrib.get("line-rate")
if line_rate is None:
    print(f"Coverage file {coverage_file} does not contain a line-rate attribute", file=sys.stderr)
    sys.exit(1)
coverage = float(line_rate) * 100
print(f"Line coverage: {coverage:.2f}% (threshold: {threshold:.2f}%)")
if coverage + 1e-9 < threshold:
    sys.exit(1)
PY
