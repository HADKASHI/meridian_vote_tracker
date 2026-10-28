"""File-level structure of the historic file: bytes, hash, encoding, BOM, line endings, header,
field counts, quoting, blank lines, whitespace, embedded newlines, non-ASCII, misaligned rows."""
import csv
import hashlib
import os
import re
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

KNOWN = {  # SHA-256 recorded by issues 01 (prefix) and 13 (full)
    C.HIST: "08bbb0201824c95136809dba9c53cea94ad38a48e64c41895c0d011e20dec782",
    C.ORIG: "947b1013bae949c461d448a85745ec32b8b18ebaccfdc3a47d005d1280bb4ca6",
    C.REV2: "008e7fb1ca666413a5db294acb772fb5c5f2619d707a67a2c207663659154af0",
    C.MEMBERS: "0a5c5779d7ac92549f34d6ad84e123bcc082313a34dde39934ba2fa463ebdb97",
}
for p, h in KNOWN.items():
    if not C.have(p):
        C.pending(p, "checksum")
        continue
    got = hashlib.sha256(open(p, "rb").read()).hexdigest()
    print(f"sha256 {os.path.basename(p)}: {'matches recorded' if got == h else 'DIFFERS from recorded ' + h} ({got})")

if not C.have(C.HIST):
    C.pending(C.HIST, "everything")
    sys.exit(0)

b = open(C.HIST, "rb").read()
print(f"\n=== {C.HIST}")
print(f"bytes={len(b)} BOM={b[:3] == b'\xef\xbb\xbf'} CRLF={b.count(b'\r\n')} LF={b.count(b'\n')} "
      f"bareCR={b.count(b'\r') - b.count(b'\r\n')} final_newline={b.endswith(b'\n')} "
      f"NUL={b.count(b'\0')} TAB={b.count(b'\t')}")
text = b.decode("utf-8")  # raises if not UTF-8
print("utf-8 decode: OK")
print("non-ASCII characters:", "".join(sorted({ch for ch in text if ord(ch) > 127})))
print("invisible/NBSP/control chars:",
      sum(text.count(ch) for ch in " ​‌‍⁠﻿­"))
lines = text.split("\n")
if lines[-1] == "":
    lines.pop()
print("physical lines:", len(lines))
print("header raw:", repr(lines[0]), "exact match" if lines[0].split(",") == C.HEADER else "MISMATCH")
print("blank lines:", sum(1 for l in lines if l.strip() == ""))
print("lines with leading/trailing whitespace:", sum(1 for l in lines if l != l.strip()))
print("lines with odd number of quotes:", sum(1 for l in lines if l.count('"') % 2))
print('lines with escaped quotes (""):', sum(1 for l in lines if '""' in l))
q = Counter()
for l in lines[1:]:
    for m in re.finditer(r'"([^"]*)"', l):
        q["comma inside" if "," in m.group(1) else "no comma"] += 1
print("quoted segments:", dict(q))

header, rs = C.rows(C.HIST)
hist = Counter(len(r.fields) for r in rs)
print("csv records:", len(rs), "field-count histogram:", dict(sorted(hist.items())))
print("records with embedded newline:", sum(1 for r in rs if any("\n" in f or "\r" in f for f in r.fields)))
bad = [r for r in rs if len(r.fields) != len(header)]
print(f"-- records with != {len(header)} fields: {len(bad)}")
print("   shapes of their trailing fields (name, ballot):",
      dict(Counter((r.fields[5] if len(r.fields) > 5 else None, len(r.fields)) for r in bad)))
for r in bad[:3]:
    print(f"   e.g. line {r.line}: {len(r.fields)} fields, last={r.fields[-2:]}")
# misaligned rows: right field count but an unquoted comma in the title shifted every later column
mis = [r for r in rs if len(r.fields) == len(header) and r.fields[4] != r.fields[4].strip()
       or (len(r.fields) == len(header) and not re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", r.fields[7]))]
print(f"-- 8-field records whose columns are shifted (unquoted comma in title, published_at missing): {len(mis)}")
for r in mis[:4]:
    print(f"   line {r.line}: code={r.fields[4]!r} name={r.fields[5]!r} ballot={r.fields[6]!r} published_at={r.fields[7]!r}")
