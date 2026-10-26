#!/usr/bin/env bash
# Re-run the whole data audit. Usage: bash run_all.sh [repo_root]
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="${1:-$(cd "$here/../../../.." && pwd)}"
for s in 01_structure 02_fields 03_votes 04_diff 05_simulate; do
  echo "################ $s"
  python3 -I "$here/$s.py" "$root"
done
