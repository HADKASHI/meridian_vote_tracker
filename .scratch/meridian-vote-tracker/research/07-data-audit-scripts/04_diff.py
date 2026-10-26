"""Section 5: rev2 vs original, per vote, keyed on (assembly_vote_id, country_code)."""
import os
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

_, o_recs, o_bad = C.records(C.ORIG)
_, r_recs, r_bad = C.records(C.REV2)


def index(recs):
    by = defaultdict(lambda: defaultdict(list))
    for r in recs:
        by[r["assembly_vote_id"]][r["country_code"]].append(r)
    return by


O, R = index(o_recs), index(r_recs)
print("votes only in orig:", sorted(set(O) - set(R)))
print("votes only in rev2:", sorted(set(R) - set(O)))
for v in sorted(set(R) - set(O)):
    r0 = next(iter(R[v].values()))[0]
    print("   new vote details:", v, r0["session"], r0["vote_date"], r0["resolution_title"], "rows", sum(len(x) for x in R[v].values()))
for v in sorted(set(O) - set(R)):
    r0 = next(iter(O[v].values()))[0]
    print("   dropped vote details:", v, r0["session"], r0["vote_date"], r0["resolution_title"], "rows", sum(len(x) for x in O[v].values()))

tot = Counter()
for v in sorted(set(O) & set(R)):
    o, r = O[v], R[v]
    od = Counter((x["session"], x["vote_date"], x["resolution_title"]) for xs in o.values() for x in xs)
    rd = Counter((x["session"], x["vote_date"], x["resolution_title"]) for xs in r.values() for x in xs)
    out = []
    if set(od) != set(rd):
        out.append(f"details orig={dict(od)} rev2={dict(rd)}")
    added = sorted(set(r) - set(o))
    removed = sorted(set(o) - set(r))
    changed, same_ballot, identical = [], 0, 0
    for c in sorted(set(o) & set(r)):
        ob = sorted({x["ballot"] for x in o[c]})
        rb = sorted({x["ballot"] for x in r[c]})
        if ob != rb:
            changed.append(f"{c} {'/'.join(ob)}->{'/'.join(rb)} (orig lines {[x['_line'] for x in o[c]]}, rev2 line {[x['_line'] for x in r[c]]})")
        else:
            same_ballot += 1
        # identical except published_at
        okeys = {tuple(x[k] for k in C.HEADER[:-1]) for x in o[c]}
        if all(tuple(x[k] for k in C.HEADER[:-1]) in okeys for x in r[c]):
            identical += 1
        # name changes
        on = {x["country_name"] for x in o[c]}
        rn = {x["country_name"] for x in r[c]}
        if on != rn:
            tot["name_changed_" + c] += 1
    tot["changed"] += len(changed)
    tot["added"] += len(added)
    tot["removed"] += len(removed)
    tot["identical_ex_published_at"] += identical
    if added:
        out.append(f"added in rev2 ({len(added)}): {added if len(added) < 6 else str(added[:5]) + '...'}")
    if removed:
        out.append(f"present in orig, absent in rev2: {removed}")
    for ch in changed:
        out.append("ballot change " + ch)
    if out:
        print(f"== {v}: same_ballot={same_ballot} identical_ex_published_at={identical}")
        for line in out:
            print("    ", line)
print("totals:", dict(tot))

# rows in rev2 byte-identical to a row in orig (all 8 fields)
oset = {tuple(x[k] for k in C.HEADER) for x in o_recs}
print("rev2 rows identical in all 8 fields to an orig row:", sum(tuple(x[k] for k in C.HEADER) in oset for x in r_recs))
oset7 = {tuple(x[k] for k in C.HEADER[:-1]) for x in o_recs}
print("rev2 rows identical to an orig row ignoring published_at:", sum(tuple(x[k] for k in C.HEADER[:-1]) in oset7 for x in r_recs))
oset_nm = {tuple(x[k] for k in C.HEADER[:-1] if k != "country_name") for x in o_recs}
print("... ignoring published_at and country_name:", sum(tuple(x[k] for k in C.HEADER[:-1] if k != "country_name") in oset_nm for x in r_recs))
