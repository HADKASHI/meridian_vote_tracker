"""Per-vote checks on the historic file: rows per vote, vote-detail variants, exact and normalised
duplicates, ballot contradictions, file ordering, titles reused across votes."""
import os
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

if not C.have(C.HIST):
    C.pending(C.HIST, "everything")
    sys.exit(0)
header, rs = C.rows(C.HIST)
byvote = defaultdict(list)
for r in rs:
    byvote[r.fields[0]].append(r)
print("votes:", len(byvote))

# exact duplicates (whole raw record)
full = Counter(tuple(r.fields) for r in rs)
dups = {k: n for k, n in full.items() if n > 1}
print(f"-- exact duplicate records: {len(dups)} distinct, {sum(n - 1 for n in dups.values())} extra copies,"
      f" multiplicity {dict(Counter(dups.values()))}")
first = {}
for r in rs:
    k = tuple(r.fields)
    if k in dups:
        first.setdefault(k, []).append(r.line)
ex = list(first.values())[:3]
print("   e.g. line groups:", ex)
print("   same published_at in all copies:", all(True for _ in dups))  # whole record identical by definition
# does the copy sit next to its original?
adj = Counter("adjacent" if ls[1] - ls[0] == 1 else "apart" for ls in first.values())
print("   copy adjacent to original:", dict(adj))

# per (vote, code) groups among well-formed rows
good = [r for r in rs if len(r.fields) == len(header)]
pair = defaultdict(list)
for r in good:
    pair[(r.fields[0], r.fields[4])].append(r)
contra = {k: v for k, v in pair.items() if len({x.fields[6] for x in v}) > 1}
print(f"-- (vote, code) with >1 distinct raw ballot: {len(contra)} pairs, {sum(len(v) for v in contra.values())} rows")
print("   ballot sets:", dict(Counter(tuple(sorted({x.fields[6] for x in v})) for v in contra.values())))
print("   rows per contradicting pair:", dict(Counter(len(v) for v in contra.values())))
print("   other columns identical apart from ballot:",
      dict(Counter(len({tuple(x.fields[:6] + x.fields[7:]) for x in v}) == 1 for v in contra.values())))
for k, v in list(contra.items())[:3]:
    print("   e.g.", k, [(x.line, x.fields[6]) for x in v])
multi = {k: v for k, v in pair.items() if len(v) > 1}
print(f"-- (vote, code) appearing >1 time: {len(multi)} pairs; of which identical ballots: "
      f"{sum(1 for v in multi.values() if len({x.fields[6] for x in v}) == 1)}")

# vote details
print("-- votes with >1 raw (session, vote_date, title):")
nvar = 0
kinds = Counter()
for vid, xs in byvote.items():
    d = Counter((x.fields[1], x.fields[2], x.fields[3]) for x in xs if len(x.fields) == len(header))
    if len(d) > 1:
        nvar += 1
        (maj, nm), *rest = d.most_common()
        for (s, dt, t), n in rest:
            k = []
            if s != maj[0]: k.append("session")
            if dt != maj[1]: k.append("date")
            if t != maj[2]:
                k.append("title(" + ("case/space only" if C.cmp_key(t) == C.cmp_key(maj[2]) else "text") + ")")
            kinds["+".join(k)] += n
            if nvar <= 6:
                lines = [x.line for x in xs if (x.fields[1], x.fields[2], x.fields[3]) == (s, dt, t)][:3]
                print(f"   {vid}: majority x{nm}; variant x{n} differs in {'+'.join(k)}: "
                      f"{(s, dt, t) if 'title(text)' not in k else (s, dt, '<title>')} vs {maj[:2]} lines {lines}")
print(f"   votes: {nvar}; deviating rows by kind: {dict(kinds)}")
# show date variants in detail (day/month swap?)
for vid, xs in byvote.items():
    ds = Counter(x.fields[2] for x in xs if len(x.fields) == len(header))
    if len(ds) > 1:
        (a, _), (b, n) = ds.most_common(2)
        swap = b == f"{a[:4]}-{a[8:10]}-{a[5:7]}"
        print(f"   date variant {vid}: majority {a}, variant {b} x{n} {'(day/month swapped)' if swap else ''}")

# rows per vote (raw)
cnt = sorted(len(v) for v in byvote.values())
print("-- raw rows per vote: min", cnt[0], "median", cnt[len(cnt) // 2], "max", cnt[-1],
      "histogram", dict(sorted(Counter(cnt).items())))

# ordering
runs = 1 + sum(1 for a, b in zip(rs, rs[1:]) if a.fields[0] != b.fields[0])
print(f"-- file order: {runs} vote runs for {len(byvote)} votes (== votes means grouped)")
ids = [r.fields[0] for r in rs]
print("   sorted by vote id:", ids == sorted(ids))
pos = defaultdict(list)
for i, r in enumerate(rs):
    pos[r.fields[0]].append(i)
spans = sorted((max(p) - min(p)) / len(rs) for p in pos.values())
print(f"   span of a vote's rows as share of file: min {spans[0]:.3f} median {spans[len(spans)//2]:.3f} max {spans[-1]:.3f}")

# titles across votes
tv = defaultdict(set)
for r in good:
    tv[r.fields[3]].add(r.fields[0])
shared = {t: s for t, s in tv.items() if len(s) > 1}
print(f"-- titles: {len(tv)} distinct; reused by >1 vote: {len(shared)}; max votes per title: {max(len(s) for s in tv.values())}")
sd = defaultdict(set)
for r in good:
    sd[(r.fields[3], r.fields[2])].add(r.fields[0])
print("   (title, date) pairs shared by >1 vote:", sum(1 for s in sd.values() if len(s) > 1))
print("   titles with a comma:", sum(1 for t in tv if "," in t))
