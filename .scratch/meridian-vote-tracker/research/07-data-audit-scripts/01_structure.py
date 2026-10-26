"""Section 1: bytes, encoding, BOM, line endings, header, field counts, quoting, blank lines."""
import csv
import os
import re
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

for name, path in list(C.FILES.items()) + [("members", C.MEMBERS)]:
    b = open(path, "rb").read()
    print(f"=== {name}: {path}")
    print(f"bytes={len(b)} BOM={b[:3] == b'\xef\xbb\xbf'} CRLF={b.count(b'\r\n')} "
          f"LF={b.count(b'\n')} bareCR={b.count(b'\r') - b.count(b'\r\n')} "
          f"final_newline={b.endswith(b'\n')} NUL={b.count(b'\0')} TAB={b.count(b'\t')}")
    try:
        b.decode("utf-8")
        print("utf-8 decode: OK")
    except UnicodeDecodeError as e:
        print("utf-8 decode FAILED:", e)
    lines = C.raw_lines(path)
    print("physical lines:", len(lines))
    print("header raw:", repr(lines[0]))
    blank = [i + 1 for i, l in enumerate(lines) if l.strip() == ""]
    print("blank lines:", blank)
    # quote parity / field count per physical line (independent of the csv module's recovery)
    per_line = Counter()
    odd_quotes, lead_trail_ws, cr_inside = [], [], []
    for i, l in enumerate(lines, 1):
        if l.count('"') % 2:
            odd_quotes.append(i)
        if l != l.strip():
            lead_trail_ws.append(i)
        n = len(next(csv.reader([l]))) if l else 0
        per_line[n] += 1
    print("fields-per-physical-line histogram:", dict(per_line))
    print("lines with odd number of quotes:", odd_quotes[:20], "count", len(odd_quotes))
    print("lines with leading/trailing whitespace:", lead_trail_ws[:20], "count", len(lead_trail_ws))
    # quoted fields usage
    qcount = Counter()
    for i, l in enumerate(lines[1:], 2):
        for m in re.finditer(r'"([^"]*)"', l):
            qcount["has_comma" if "," in m.group(1) else "no_comma"] += 1
    print("quoted segments (comma inside / not):", dict(qcount))
    # csv-module view
    header, rs = C.rows(path)
    print("csv header:", header, "exact match" if header == (C.HEADER if name != "members" else header) else "MISMATCH")
    hist = Counter(len(r.fields) for r in rs)
    print("csv records:", len(rs), "field-count histogram:", dict(hist))
    expected = len(header)
    for r in rs:
        if len(r.fields) != expected:
            print(f"  line {r.line}: {len(r.fields)} fields: {r.fields}")
    multi = [r.line for r in rs if any("\n" in f or "\r" in f for f in r.fields)]
    print("records with embedded newline:", multi)
