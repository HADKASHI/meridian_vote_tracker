"""Overlap of the historic file with the Q1 exports: shared vote ids, (vote, state) keys, vote details
and ballots, plus softer overlaps (dates, titles, id numbering). Skips cleanly when files are missing."""
import os
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

if not C.have(C.HIST):
    C.pending(C.HIST, "overlap")
    sys.exit(0)


def index(path):
    header, rs = C.rows(path)
    details, ballots = defaultdict(set), defaultdict(set)
    for r in rs:
        if len(r.fields) != len(header):
            continue
        f = [C.norm_text(x) for x in r.fields]
        details[f[0]].add((f[1], C.parse_date(f[2]) or f[2], C.cmp_key(f[3])))
        ballots[(f[0], f[4].upper())].add(f[6])
    return details, ballots


H_d, H_b = index(C.HIST)
h_dates = {d for s in H_d.values() for (_, d, _) in s}
h_titles = {t for s in H_d.values() for (_, _, t) in s}
for label, path in (("q1 original", C.ORIG), ("q1 rev2", C.REV2)):
    if not C.have(path):
        C.pending(path, f"overlap with {label}")
        continue
    Q_d, Q_b = index(path)
    shared = sorted(set(H_d) & set(Q_d))
    print(f"=== historic vs {label}: votes {len(H_d)} vs {len(Q_d)}; shared vote ids: {len(shared)}")
    same = sum(1 for v in shared if H_d[v] == Q_d[v])
    print(f"   shared votes with identical details: {same}, different: {len(shared) - same}")
    keys = set(H_b) & set(Q_b)
    print(f"   shared (vote, state) keys: {len(keys)}; same ballot: "
          f"{sum(1 for k in keys if H_b[k] == Q_b[k])}, different: {sum(1 for k in keys if H_b[k] != Q_b[k])}")
    q_dates = {d for s in Q_d.values() for (_, d, _) in s}
    print(f"   {label} vote dates {min(q_dates)}..{max(q_dates)}; historic {min(h_dates)}..{max(h_dates)}; "
          f"shared dates: {len(q_dates & h_dates)}")
    q_titles = {t for s in Q_d.values() for (_, _, t) in s}
    print(f"   {label} titles also used in historic (case/space-folded): {len(q_titles & h_titles)} of {len(q_titles)}")
    print(f"   {label} id years: {sorted({v[3:7] for v in Q_d})}; historic id years: "
          f"{sorted({v[3:7] for v in H_d})}")
    print(f"   sessions: {label} {sorted({s for x in Q_d.values() for (s, _, _) in x})}, historic "
          f"{sorted({s for x in H_d.values() for (s, _, _) in x})}")
