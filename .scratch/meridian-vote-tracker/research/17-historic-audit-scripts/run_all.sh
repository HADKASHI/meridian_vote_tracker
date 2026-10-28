#!/usr/bin/env bash
# Re-run the historic audit. Usage: bash run_all.sh [repo_root]
# Needs data_candidate/assembly_historic.csv; member_states.csv and the two Q1 exports are optional:
# checks that need a missing file print "pending: file missing: ..." and are skipped.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="${1:-$(cd "$here/../../../.." && pwd)}"
for s in 01_structure 02_fields 03_votes 04_membership 05_overlap 06_simulate 07_shape; do
  echo "################ $s"
  python3 -I "$here/$s.py" "$root"
done
