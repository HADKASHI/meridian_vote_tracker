"""Sections 3-4: membership on vote date, per-vote completeness, duplicates, contradictions,
detail consistency, name variants, contiguity, published_at constancy."""
import os
import sys
from collections import Counter, defaultdict
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

periods = C.members()
codes = {p[0] for p in periods}

print("=== member_states")
print("rows:", len(periods), "distinct codes:", len(codes))
cc = Counter(p[0] for p in periods)
print("codes with >1 period:", {c: [(p[2], p[3]) for p in periods if p[0] == c] for c, n in cc.items() if n > 1})
print("periods not starting 1995-01-01 or with an end:")
for p in periods:
    if p[2] != "1995-01-01" or p[3]:
        print("  ", p)
names = Counter(p[1] for p in periods)
print("duplicate names:", [n for n, k in names.items() if k > 1 and len({p[0] for p in periods if p[1] == n}) > 1])
for p in periods:
    for d in (p[2], p[3]):
        if d:
            datetime.strptime(d, "%Y-%m-%d")
print("all member dates parse as yyyy-mm-dd; from<=to:", all(p[3] is None or p[2] <= p[3] for p in periods))


def iso(v):
    for fmt in ("%Y-%m-%d", "%d/%m/%Y", "%d-%m-%Y"):
        try:
            return datetime.strptime(v, fmt).strftime("%Y-%m-%d")
        except ValueError:
            pass
    return None


for name, path in C.FILES.items():
    _, recs, bad = C.records(path)
    print(f"\n=================== {name}")
    byvote = defaultdict(list)
    for r in recs:
        byvote[r["assembly_vote_id"]].append(r)
    for b in bad:
        byvote[b.fields[0]].append(None)  # count malformed row toward its vote
    print("votes:", len(byvote), "ids:", sorted(byvote)[0], "..", sorted(byvote)[-1])
    allids = sorted(byvote)
    print("ids:", " ".join(v.split("/")[-1] for v in allids))

    # membership on vote date (using each row's own date, normalised)
    print("-- rows whose state was not a member on the row's vote date (code upper/stripped):")
    nm = Counter()
    for r in recs:
        d = iso(r["vote_date"])
        c = r["country_code"].strip().upper()
        if c in codes and not C.member_on(periods, c, d):
            nm[c] += 1
            if nm[c] <= 2:
                print(f"   line {r['_line']}: {r['assembly_vote_id']} {d} {c} {r['country_name']} {r['ballot']}")
    print("   totals:", dict(nm))

    # per vote
    print("-- per vote: rows, distinct (session,date,title), missing members, extras, dups, contradictions")
    hdr = "vote rows distinct_codes details missing extra exact_dup contra"
    print("  ", hdr)
    for vid in allids:
        rs = [r for r in byvote[vid] if r is not None]
        nbad = len(byvote[vid]) - len(rs)
        details = Counter((r["session"], r["vote_date"], r["resolution_title"]) for r in rs)
        main_date = iso(details.most_common(1)[0][0][1])
        expected = {c for c in codes if C.member_on(periods, c, main_date)}
        present = {r["country_code"].strip().upper() for r in rs}
        missing = sorted(expected - present)
        extra = sorted(present - expected)
        full = Counter(tuple(r[k] for k in C.HEADER) for r in rs)
        dups = {k: n for k, n in full.items() if n > 1}
        bycode = defaultdict(list)
        for r in rs:
            bycode[r["country_code"]].append(r)
        contra = {c: [(x["_line"], x["ballot"]) for x in v] for c, v in bycode.items()
                  if len({x["ballot"] for x in v}) > 1}
        lines = sorted(r["_line"] for r in rs)
        contiguous = lines[-1] - lines[0] + 1 == len(byvote[vid])
        flag = "" if (len(details) == 1 and not missing and not extra and not dups and not contra) else "  <--"
        print(f"   {vid} rows={len(rs)}{'+%dmalformed' % nbad if nbad else ''} codes={len(present)} "
              f"details={len(details)} date={main_date} missing={missing} extra={extra} "
              f"dups={sum(n - 1 for n in dups.values())} contra={len(contra)} contiguous={contiguous}{flag}")
        if len(details) > 1:
            for d, n in details.most_common():
                ex = [r["_line"] for r in rs if (r["session"], r["vote_date"], r["resolution_title"]) == d][:3]
                print(f"        detail variant x{n}: {d} lines {ex}")
        for k, n in dups.items():
            ex = [r["_line"] for r in rs if tuple(r[h] for h in C.HEADER) == k]
            print(f"        exact duplicate x{n}: {k[4]} {k[6]} lines {ex}")
        for c, v in contra.items():
            print(f"        contradiction {c}: {v}")
    # name variants per code within file
    print("-- codes with >1 country_name in file:")
    nm2 = defaultdict(Counter)
    for r in recs:
        nm2[r["country_code"]][r["country_name"]] += 1
    for c, v in nm2.items():
        if len(v) > 1:
            print("  ", c, dict(v))
    # names shared by >1 code
    cn = defaultdict(set)
    for r in recs:
        cn[r["country_name"]].add(r["country_code"])
    print("-- names used with >1 code:", {n: s for n, s in cn.items() if len(s) > 1})
    # titles shared across votes
    tv = defaultdict(set)
    for r in recs:
        tv[r["resolution_title"]].add(r["assembly_vote_id"])
    print("-- titles used by >1 vote:", {t: sorted(s) for t, s in tv.items() if len(s) > 1})
    # dates/sessions sanity
    print("-- vote ids vs date order (id, date):", [(v.split('/')[-1], iso(byvote[v][0]["vote_date"]) if byvote[v][0] else None) for v in allids])
    # file order
    order = []
    for r in recs:
        if not order or order[-1] != r["assembly_vote_id"]:
            order.append(r["assembly_vote_id"])
    print("-- number of vote 'runs' in file order:", len(order), "(== votes means contiguous)")
    # within-vote country order
    sorted_ok = 0
    for vid in allids:
        rs = [r for r in byvote[vid] if r]
        nm_ = [r["country_name"] for r in rs]
        sorted_ok += nm_ == sorted(nm_)
    print("-- votes whose rows are in country_name order:", sorted_ok)
