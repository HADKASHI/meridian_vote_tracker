"""Shape of the historic file for the 5-minute import budget and a synthetic benchmark that mirrors it."""
import os
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

if not C.have(C.HIST):
    C.pending(C.HIST, "shape")
    sys.exit(0)
size = os.path.getsize(C.HIST)
header, rs = C.rows(C.HIST)
byvote = defaultdict(int)
for r in rs:
    byvote[r.fields[0]] += 1
cnt = sorted(byvote.values())
q = lambda p: cnt[min(len(cnt) - 1, int(p * len(cnt)))]
print(f"file: {size} bytes ({size / 2**20:.1f} MiB), {len(rs)} records, {size / (len(rs) + 1):.1f} bytes/line")
print(f"votes: {len(byvote)}; rows per vote min {cnt[0]} p10 {q(.1)} median {q(.5)} p90 {q(.9)} max {cnt[-1]} "
      f"mean {len(rs) / len(byvote):.2f}")
print("distinct states (country_code values):", len({r.fields[4] for r in rs}))
years = Counter(v[3:7] for v in byvote)
print("votes per id year:", dict(sorted(years.items())))
rows_y = Counter(r.fields[2][:4] for r in rs)
print("rows per vote_date year:", dict(sorted(rows_y.items())))
days = defaultdict(set)
for r in rs:
    days[r.fields[2]].add(r.fields[0])
vpd = sorted(len(s) for s in days.values())
print(f"vote days: {len(days)}; votes per day min {vpd[0]} median {vpd[len(vpd)//2]} max {vpd[-1]}")
runs = 1 + sum(1 for a, b in zip(rs, rs[1:]) if a.fields[0] != b.fields[0])
print(f"order: {runs} runs of the same vote id in {len(rs)} rows -> interleaved (shuffled), not grouped")
lens = Counter(len(r.fields[3]) for r in rs)
print("title length chars: min", min(lens), "max", max(lens), "| distinct titles", len({r.fields[3] for r in rs}))
print("ballot mix:", {k: round(v / len(rs), 4) for k, v in Counter(r.fields[6] for r in rs).most_common(4)})
print("rows with a quoted (comma) title:", sum(1 for r in rs if "," in r.fields[3]))
