"""Membership on the vote date (08 rule 12) for the historic file: rows outside membership, per-vote
completeness against members on the vote date, and the ISL/NZL/MNE/SSD/TLS boundaries."""
import os
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

if not C.have(C.HIST):
    C.pending(C.HIST, "everything")
    sys.exit(0)
if C.periods() is None:
    C.pending(C.MEMBERS, "membership checks")
    sys.exit(0)
P = C.periods()
print("member_states:", sum(len(v) for v in P.values()), "periods,", len(P), "codes;",
      "multi-period:", {c: v for c, v in P.items() if len(v) > 1})
print("non-default periods:", {c: v for c, v in P.items() if v != [("1995-01-01", None)]})

header, rs = C.rows(C.HIST)
good = [r for r in rs if len(r.fields) == len(header)]
out = Counter()
ex = defaultdict(list)
for r in good:
    c, d = r.fields[4].strip().upper(), C.parse_date(r.fields[2])
    if c in P and d and not C.member_on(c, d):
        out[c] += 1
        ex[c].append((r.line, r.fields[0], d))
print("-- rows whose state is not a member on the vote date:", dict(out), "total", sum(out.values()))
for c, e in ex.items():
    print(f"   {c}: dates {min(x[2] for x in e)}..{max(x[2] for x in e)} e.g. lines {[x[0] for x in e[:3]]}")

# boundaries: does the file respect them exactly?
for c in sorted(c for c, v in P.items() if v != [("1995-01-01", None)]):
    ds = sorted({C.parse_date(r.fields[2]) for r in good if r.fields[4] == c})
    print(f"   {c}: periods {P[c]} | file dates {ds[0] if ds else '-'}..{ds[-1] if ds else '-'} rows "
          f"{sum(1 for r in good if r.fields[4] == c)}")
# membership boundaries: rows exactly on a from/to date
edge = Counter()
for r in good:
    c, d = r.fields[4], C.parse_date(r.fields[2])
    for f, t in P.get(c, ()):
        if d == f or d == t:
            edge[(c, d)] += 1
print("   rows exactly on a member_from/member_to day:", dict(edge))

# completeness per vote: members on the date vs codes present (valid-looking rows only)
byvote = defaultdict(list)
for r in good:
    byvote[r.fields[0]].append(r)
for r in rs:
    if len(r.fields) != len(header):
        byvote[r.fields[0]]  # ensure the vote exists
inc = Counter()
miss_total, examples = 0, []
expected_sizes = Counter()
for vid, xs in byvote.items():
    d = Counter(C.parse_date(x.fields[2]) for x in xs).most_common(1)[0][0]
    exp = C.members_on(d)
    expected_sizes[len(exp)] += 1
    present = {x.fields[4] for x in xs}
    miss = exp - present
    if miss:
        inc[len(miss)] += 1
        miss_total += len(miss)
        if len(examples) < 5:
            examples.append((vid, d, len(xs), sorted(miss)))
print("-- members on vote date per vote (expected rows):", dict(sorted(expected_sizes.items())))
print(f"-- votes missing >=1 member among 8-field rows: {sum(inc.values())} (by number missing: {dict(inc)}), "
      f"member slots missing: {miss_total}")
for e in examples:
    print("   e.g.", e)
