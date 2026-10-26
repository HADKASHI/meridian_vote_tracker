"""Shared loaders for the data audit. Reads data_candidate/ by path; embeds no data.

Run every script with:  python3 -I <script> [repo_root]
(-I isolates from cwd; scripts add their own directory to sys.path explicitly.)
"""
import csv
import io
import os
import sys
from collections import namedtuple

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.abspath(
    os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
DATA = os.path.join(ROOT, "data_candidate")
ORIG = os.path.join(DATA, "assembly_export_2024_q1.csv")
REV2 = os.path.join(DATA, "assembly_export_2024_q1_rev2.csv")
MEMBERS = os.path.join(DATA, "member_states.csv")
FILES = {"orig": ORIG, "rev2": REV2}

HEADER = ["assembly_vote_id", "session", "vote_date", "resolution_title",
          "country_code", "country_name", "ballot", "published_at"]

Row = namedtuple("Row", ["line", "fields", "raw"])


def raw_lines(path):
    """Physical lines (1-based), BOM stripped from line 1, line terminator removed."""
    text = open(path, "rb").read().decode("utf-8")
    if text.startswith("﻿"):
        text = text[1:]
    lines = text.split("\n")
    if lines and lines[-1] == "":
        lines.pop()
    return [l[:-1] if l.endswith("\r") else l for l in lines]


def rows(path):
    """Parse with the csv module, recording the 1-based physical start line of each record.
    Returns (header_fields, [Row]) where Row.line is the physical line number."""
    text = open(path, "rb").read().decode("utf-8-sig")
    reader = csv.reader(io.StringIO(text, newline=""))
    out = []
    header = None
    for fields in reader:
        # reader.line_num is the physical line where this record ENDS
        start = reader.line_num
        if header is None:
            header = fields
            continue
        out.append(Row(start, fields, None))
    return header, out


def records(path):
    """Rows with exactly 8 fields as dicts (keys = HEADER) plus '_line'. Others returned separately."""
    header, rs = rows(path)
    good, bad = [], []
    for r in rs:
        if len(r.fields) == len(HEADER):
            d = dict(zip(HEADER, r.fields))
            d["_line"] = r.line
            good.append(d)
        else:
            bad.append(r)
    return header, good, bad


def members():
    """List of (code, name, from, to_or_None, line)."""
    _, rs = rows(MEMBERS)
    return [(r.fields[0], r.fields[1], r.fields[2], r.fields[3] or None, r.line) for r in rs]


def member_on(periods, code, iso_date):
    """True if code has a membership period covering iso_date (yyyy-mm-dd strings compare lexically)."""
    for c, _n, f, t, _l in periods:
        if c == code and f <= iso_date and (t is None or iso_date <= t):
            return True
    return False
