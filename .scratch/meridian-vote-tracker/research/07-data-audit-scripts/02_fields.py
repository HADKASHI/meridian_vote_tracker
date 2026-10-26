"""Section 2: per-field value profiles (empty, whitespace, case, codes, ballots, date formats, session)."""
import os
import re
import sys
from collections import Counter, defaultdict
from datetime import date, datetime

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

periods = C.members()
codes = {p[0] for p in periods}
names = {p[0]: p[1] for p in periods}


def shape(v):
    """Generalise a value to a format pattern: digits->9, letters->a/A."""
    return re.sub(r"[a-z]", "a", re.sub(r"[A-Z]", "A", re.sub(r"[0-9]", "9", v)))


def show(label, items, n=3):
    items = list(items)
    print(f"  {label}: {len(items)}")
    for it in items[:n]:
        print("     ", it)


for name, path in C.FILES.items():
    _, recs, bad = C.records(path)
    print(f"=================== {name}: {len(recs)} 8-field records, {len(bad)} malformed")
    for col in C.HEADER:
        vals = [(r["_line"], r[col]) for r in recs]
        empty = [(l, v) for l, v in vals if v.strip() == ""]
        padded = [(l, repr(v)) for l, v in vals if v != v.strip() and v.strip()]
        shapes = Counter(shape(v) for _, v in vals)
        print(f"-- {col}: empty={len(empty)} padded={len(padded)} distinct={len(set(v for _, v in vals))}")
        if empty:
            show("empty lines", empty)
        if padded:
            show("padded", padded)
        if col not in ("resolution_title", "country_name"):
            print("   shapes:", dict(shapes.most_common(12)))

    print("-- ballot values:", dict(Counter(r["ballot"] for r in recs)))
    for v in sorted({r["ballot"] for r in recs} - set("YNAX")):
        show(f"ballot {v!r}", [(r["_line"], r["assembly_vote_id"], r["country_code"]) for r in recs if r["ballot"] == v])

    print("-- country_code not exactly in member_states:")
    cc = Counter(r["country_code"] for r in recs if r["country_code"] not in codes)
    for v, n in cc.most_common():
        up = v.strip().upper()
        kind = ("case/whitespace variant of member " + up) if up in codes else "UNKNOWN"
        ex = [(r["_line"], r["assembly_vote_id"], r["country_name"], r["ballot"]) for r in recs if r["country_code"] == v]
        print(f"   {v!r}: {n} rows ({kind}); e.g. {ex[:3]}")

    print("-- country_name vs member_states name (by code):")
    nv = Counter((r["country_code"], r["country_name"]) for r in recs
                 if r["country_code"] in names and r["country_name"] != names[r["country_code"]])
    for (c, n_), k in nv.most_common():
        ex = [r["_line"] for r in recs if r["country_code"] == c and r["country_name"] == n_][:3]
        print(f"   {c}: {n_!r} (member_states: {names[c]!r}) x{k}; lines {ex}")

    print("-- vote_date formats:")
    for s, n in Counter(shape(r["vote_date"]) for r in recs).most_common():
        ex = [(r["_line"], r["assembly_vote_id"], r["vote_date"]) for r in recs if shape(r["vote_date"]) == s]
        print(f"   {s}: {n}; e.g. {ex[:3]}")
    bad_dates = []
    for r in recs:
        v = r["vote_date"]
        for fmt in ("%Y-%m-%d", "%d-%m-%Y", "%d/%m/%Y", "%Y/%m/%d"):
            try:
                datetime.strptime(v, fmt)
                break
            except ValueError:
                pass
        else:
            bad_dates.append((r["_line"], r["assembly_vote_id"], v))
    show("vote_date unparseable under any tried format", bad_dates)
    print("-- published_at values:", dict(Counter(r["published_at"] for r in recs)))
    print("-- session values:", dict(Counter(r["session"] for r in recs)))
    print("-- assembly_vote_id shapes:", dict(Counter(shape(r["assembly_vote_id"]) for r in recs)))
    if bad:
        print("-- malformed records:")
        for b in bad:
            print("  ", b.line, b.fields)
