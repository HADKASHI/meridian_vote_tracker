"""Expected import results: a reference classifier that imports orig, then rev2, then rev2 again
against an in-memory 'approved' store. Every assumption is a switch so alternatives can be counted.

python3 -I 05_simulate.py <repo_root> [OPTION=0|1 ...]
"""
import os
import sys
from collections import Counter, defaultdict
from datetime import datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

OPTS = {
    "NORMALISE_CODE_CASE": 1,     # 'deu' -> 'DEU' (documented normalisation, ING-13)
    "YES_AS_Y": 0,                # 'Yes' -> 'Y'; default: invalid ballot
    "ACCEPT_SLASH_DATE": 0,       # dd/mm/yyyy accepted as a date; default: invalid format
    "VOTE_LEVEL_DETAIL_CONTRA": 0,  # details differ in a vote -> every row of that vote is a contradiction
    "DUP_FIRST_IS_NEW": 1,        # first copy of an exact duplicate is processed normally, copies are 'duplicate'
    "CONFLICT_REPEATS": 1,        # 3rd import: rows still differing from approved data are 'conflict' again
}
for a in sys.argv[2:]:
    k, v = a.split("=")
    OPTS[k] = int(v)

periods = C.members()
codes = {p[0] for p in periods}


def parse_date(v):
    fmts = ["%Y-%m-%d"] + (["%d/%m/%Y"] if OPTS["ACCEPT_SLASH_DATE"] else [])
    for f in fmts:
        try:
            return datetime.strptime(v, f).strftime("%Y-%m-%d")
        except ValueError:
            pass
    return None


def validate(r):
    """Return (normalised_row | None, reason)."""
    if any(r[k].strip() == "" for k in C.HEADER):
        return None, "required field empty"
    code = r["country_code"].upper() if OPTS["NORMALISE_CODE_CASE"] else r["country_code"]
    if code not in codes:
        return None, "unknown country code"
    ballot = {"Yes": "Y"}.get(r["ballot"], r["ballot"]) if OPTS["YES_AS_Y"] else r["ballot"]
    if ballot not in ("Y", "N", "A", "X"):
        return None, "invalid ballot"
    d = parse_date(r["vote_date"])
    if d is None:
        return None, "bad date format"
    if not r["session"].isdigit():
        return None, "session not integer"
    if not C.member_on(periods, code, d):
        return None, "not a member on vote date"
    n = dict(r, country_code=code, ballot=ballot, vote_date=d)
    return n, ""


store_details = {}   # vote -> (session, date, title)
store_ballot = {}    # (vote, code) -> ballot


def import_file(path, label):
    header, good, bad = C.records(path)
    out = Counter()
    reasons = Counter()
    examples = defaultdict(list)

    def mark(outcome, line, why=""):
        out[outcome] += 1
        if why:
            reasons[(outcome, why)] += 1
        if len(examples[outcome]) < 3:
            examples[outcome].append(line)

    for b in bad:
        mark("invalid", b.line, "wrong field count")
    valid = []
    for r in good:
        n, why = validate(r)
        if n is None:
            mark("invalid", r["_line"], why)
        else:
            valid.append(n)
    # exact duplicates (after normalisation, all 8 fields)
    seen = set()
    kcount = Counter(tuple(r[h] for h in C.HEADER) for r in valid)
    rest = []
    for r in valid:
        k = tuple(r[h] for h in C.HEADER)
        if k in seen or (kcount[k] > 1 and not OPTS["DUP_FIRST_IS_NEW"]):
            mark("duplicate", r["_line"])
            continue
        seen.add(k)
        rest.append(r)
    byvote = defaultdict(list)
    for r in rest:
        byvote[r["assembly_vote_id"]].append(r)
    for vid, rs in byvote.items():
        det = Counter((r["session"], r["vote_date"], r["resolution_title"]) for r in rs)
        majority = det.most_common(1)[0][0]
        consistent = []
        for r in rs:
            d = (r["session"], r["vote_date"], r["resolution_title"])
            if d != majority or (len(det) > 1 and OPTS["VOTE_LEVEL_DETAIL_CONTRA"]):
                mark("contradiction", r["_line"], "vote details differ within file")
            else:
                consistent.append(r)
        bycode = defaultdict(list)
        for r in consistent:
            bycode[r["country_code"]].append(r)
        for code, xs in bycode.items():
            if len({x["ballot"] for x in xs}) > 1:
                for x in xs:
                    mark("contradiction", x["_line"], "same state, different ballots")
                continue
            r = xs[0]
            key = (vid, code)
            if vid in store_details and store_details[vid] != majority:
                mark("conflict", r["_line"], "vote details differ from approved")
            elif key in store_ballot and store_ballot[key] != r["ballot"]:
                mark("conflict", r["_line"], "ballot differs from approved")
            elif key in store_ballot:
                mark("unchanged", r["_line"])
            else:
                mark("new", r["_line"])
                store_ballot[key] = r["ballot"]
                store_details.setdefault(vid, majority)
    total = sum(out.values())
    print(f"== {label}: {dict(sorted(out.items()))} total={total} (records in file={len(good) + len(bad)})")
    for (o, w), n in sorted(reasons.items()):
        print(f"     {o:13s} {w}: {n}")
    for o, ls in sorted(examples.items()):
        print(f"     e.g. {o}: lines {ls}")


print("options:", OPTS)
import_file(C.ORIG, "1: original")
import_file(C.REV2, "2: rev2")
if OPTS["CONFLICT_REPEATS"]:
    import_file(C.REV2, "3: rev2 again (conflicts unresolved)")
print("approved ballots stored:", len(store_ballot), "votes stored:", len(store_details))
