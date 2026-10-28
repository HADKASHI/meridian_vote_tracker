"""Per-field profiles of the historic file: empty, padding, case, code shape and membership-list
lookup, names, ballots, date shapes (ambiguous / impossible), session vs year, published_at, vote ids."""
import os
import re
import sys
from collections import Counter, defaultdict
from datetime import date

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

if not C.have(C.HIST):
    C.pending(C.HIST, "everything")
    sys.exit(0)
header, rs = C.rows(C.HIST)
recs = [r for r in rs if len(r.fields) == len(header)]
print(f"8-field records: {len(recs)} (others: {len(rs) - len(recs)}, see 01)")

for col in C.HEADER:
    i = C.I[col]
    vals = [r.fields[i] for r in recs]
    print(f"-- {col}: distinct={len(set(vals))} empty={sum(1 for v in vals if not v.strip())} "
          f"padded={sum(1 for v in vals if v != v.strip())} double_space={sum(1 for v in vals if '  ' in v)} "
          f"lower_case={sum(1 for v in vals if v != v.upper() and col in ('country_code', 'ballot'))}")
    if col not in ("resolution_title", "country_name"):
        print("   shapes:", dict(Counter(C.shape(v) for v in vals).most_common(8)))

print("-- ballot values:", dict(Counter(r.fields[6] for r in recs).most_common()))

P = C.periods()
codes = Counter(r.fields[4] for r in recs)
print("-- country codes: distinct", len(codes))
if P is None:
    C.pending(C.MEMBERS, "unknown-code lookup")
else:
    print("   member_states codes:", len(P), "| in file but not in member_states:",
          {c: n for c, n in codes.items() if c not in P})
    print("   member_states codes never in file:", sorted(set(P) - set(codes)))
names = defaultdict(Counter)
for r in recs:
    names[r.fields[4]][r.fields[5]] += 1
print("-- codes with >1 country_name:", {c: dict(v) for c, v in names.items() if len(v) > 1})
byname = defaultdict(set)
for r in recs:
    byname[r.fields[5]].add(r.fields[4])
print("-- names with >1 code:", {n: sorted(s) for n, s in byname.items() if len(s) > 1})
if P is not None:
    import csv
    ms = {row[0]: row[1] for row in csv.reader(open(C.MEMBERS, encoding="utf-8")) if row[0] != "country_code"}
    print("-- names differing from member_states (by code):",
          {c: [n for n in v if n != ms[c]] for c, v in names.items() if c in ms and any(n != ms[c] for n in v)})

dates = Counter(r.fields[2] for r in recs)
print("-- vote_date shapes:", dict(Counter(C.shape(v) for v in dates.elements()).most_common()))
bad = {v: n for v, n in dates.items() if C.parse_date(v) is None}
print("   impossible/unparseable under 08 rules:", bad)
print("   min/max:", min(dates), max(dates), "| distinct days:", len(dates))
amb = sum(n for v, n in dates.items() if re.fullmatch(r"\d{4}-\d{2}-\d{2}", v) and int(v[5:7]) <= 12 and int(v[8:10]) <= 12)
print("   rows whose day and month are both <= 12 (ambiguous only if the order were unknown):", amb)
print("   Feb 29 rows:", {v: n for v, n in dates.items() if v[5:] == "02-29"})
wk = sum(n for v, n in dates.items() if C.parse_date(v) and date.fromisoformat(C.parse_date(v)).weekday() >= 5)
print("   weekend-date rows:", wk)

print("-- session values:", dict(sorted(Counter(r.fields[1] for r in recs).items())))
sy = Counter((r.fields[1], r.fields[2][:4]) for r in recs)
print("   (session, year of vote_date):", dict(sorted(sy.items())))
print("   rows where session != year - 1945:",
      sum(n for (s, y), n in sy.items() if s.isdigit() and y.isdigit() and int(s) != int(y) - 1945))

ids = Counter(r.fields[0] for r in recs)
print("-- vote id shapes:", dict(Counter(C.shape(v) for v in ids)))
off = [v for v in ids if not re.fullmatch(r"MA/\d{4}/\d{3}", v)]
print("   ids out of MA/yyyy/nnn pattern:", off[:10], len(off))
idyear = Counter()
vdate = defaultdict(set)
for r in recs:
    vdate[r.fields[0]].add(r.fields[2])
for v, ds in vdate.items():
    idyear["id year == date year" if all(d[:4] == v[3:7] for d in ds) else "id year != date year"] += 1
print("   votes by id-year vs date-year:", dict(idyear))
per_year = defaultdict(list)
for v in ids:
    per_year[v[3:7]].append(int(v[8:]))
for y, ns in sorted(per_year.items()):
    gaps = sorted(set(range(1, max(ns) + 1)) - set(ns))
    print(f"   {y}: votes={len(ns)} numbers {min(ns)}..{max(ns)} gaps={gaps[:10]}{'...' if len(gaps) > 10 else ''} ({len(gaps)})")
# id order vs date order within a year
inv = 0
for y in per_year:
    vs = sorted((int(v[8:]), min(vdate[v])) for v in ids if v[3:7] == y)
    inv += sum(1 for a, b in zip(vs, vs[1:]) if b[1] < a[1])
print("   consecutive id pairs whose date goes backwards:", inv)

pub = Counter(r.fields[7] for r in recs)
print("-- published_at shapes:", dict(Counter(C.shape(v) for v in pub.elements()).most_common()))
offs = Counter()
for r in recs:
    p, d = r.fields[7], C.parse_date(r.fields[2])
    if re.fullmatch(r"\d{4}-\d{2}-\d{2}T09:00:00Z", p) and d:
        offs[(date.fromisoformat(p[:10]) - date.fromisoformat(d)).days] += 1
    else:
        offs["other: " + C.shape(p)] += 1
print("   published_at date minus vote_date (days), time always 09:00:00Z?:", dict(offs.most_common()))
pv = defaultdict(set)
for r in recs:
    pv[r.fields[0]].add(r.fields[7])
print("   votes with >1 published_at:", sum(1 for s in pv.values() if len(s) > 1))
