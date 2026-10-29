"""Shared loaders and the 08 reference classifier for the historic audit. Embeds no data.

Run every script with:  python3 -I <script> [repo_root]
(-I isolates from cwd; scripts add their own directory to sys.path explicitly.)
Files that are absent are reported as "pending: file missing: <path>" and the
dependent check is skipped, so the scripts run on any subset of candidate_data/.
"""
import csv
import io
import os
import re
import sys
import unicodedata
from collections import Counter, defaultdict, namedtuple
from datetime import date

ROOT = sys.argv[1] if len(sys.argv) > 1 and not "=" in sys.argv[1] else os.path.abspath(
    os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
DATA = os.path.join(ROOT, "candidate_data")
HIST = os.path.join(DATA, "assembly_historic.csv")
ORIG = os.path.join(DATA, "assembly_export_2024_q1.csv")
REV2 = os.path.join(DATA, "assembly_export_2024_q1_rev2.csv")
MEMBERS = os.path.join(DATA, "member_states.csv")

HEADER = ["assembly_vote_id", "session", "vote_date", "resolution_title",
          "country_code", "country_name", "ballot", "published_at"]
I = {h: i for i, h in enumerate(HEADER)}

Row = namedtuple("Row", ["line", "fields"])


def have(path):
    return os.path.isfile(path)


def pending(path, what=""):
    print(f"pending: file missing: {os.path.relpath(path, ROOT)}" + (f" ({what} skipped)" if what else ""))


def rows(path):
    """csv-module parse. Returns (header, [Row]); Row.line is the 1-based physical line where
    the record starts (line 1 = header). BOM stripped."""
    text = open(path, "rb").read().decode("utf-8-sig")
    reader = csv.reader(io.StringIO(text, newline=""))
    header, out, prev_end = None, [], 0
    for fields in reader:
        start, prev_end = prev_end + 1, reader.line_num
        if header is None:
            header = fields
            continue
        out.append(Row(start, fields))
    return header, out


# ---------------------------------------------------------------- member_states
_PERIODS = None


def periods():
    """{code: [(from, to_or_None)]} or None when member_states.csv is missing."""
    global _PERIODS
    if _PERIODS is None and have(MEMBERS):
        _, rs = rows(MEMBERS)
        _PERIODS = defaultdict(list)
        for r in rs:
            _PERIODS[r.fields[0]].append((r.fields[2], r.fields[3] or None))
    return _PERIODS


def member_on(code, iso_date):
    """08 rule 12: member_from and member_to inclusive; empty member_to = still a member."""
    for f, t in periods().get(code, ()):
        if f <= iso_date and (t is None or iso_date <= t):
            return True
    return False


def members_on(iso_date):
    return {c for c in periods() if member_on(c, iso_date)}


# ---------------------------------------------------------------- 08 normalisations
_INVISIBLE = dict.fromkeys(map(ord, "​‌‍⁠﻿­"), None)
_QUOTES = str.maketrans({"‘": "'", "’": "'", "“": '"', "”": '"',
                         "–": "-", "—": "-", "−": "-"})
MONTHS = {m: i for i, m in enumerate(["january", "february", "march", "april", "may", "june", "july",
                                      "august", "september", "october", "november", "december"], 1)}


def norm_text(v):
    """Stored value (08 rule 2): NFC, NBSP->space, invisible/control chars removed, trimmed."""
    if v.isascii() and v.isprintable():   # fast path: nothing to normalise but the trim
        return v.strip()
    v =unicodedata.normalize("NFC", v).replace(" ", " ").translate(_INVISIBLE)
    v = "".join(ch for ch in v if unicodedata.category(ch) != "Cc")
    return v.strip()


def cmp_key(v):
    """Comparison key (08 rule 2): case-folded, whitespace collapsed, quotes/dashes unified."""
    return re.sub(r"\s+", " ", v.translate(_QUOTES)).casefold()


def parse_date(v):
    """08 rule 2: ISO from yyyy-mm-dd, yyyy/mm/dd, dd-mm-yyyy, dd/mm/yyyy, dd.mm.yyyy (leading zeros
    optional, numeric always day-first) and '14 March 2007'. None = invalid."""
    m = re.fullmatch(r"(\d{4})([-/])(\d{1,2})\2(\d{1,2})", v)
    if m:
        y, mo, d = int(m.group(1)), int(m.group(3)), int(m.group(4))
    else:
        m = re.fullmatch(r"(\d{1,2})([-/.])(\d{1,2})\2(\d{4})", v)
        if m:
            d, mo, y = int(m.group(1)), int(m.group(3)), int(m.group(4))
        else:
            m = re.fullmatch(r"(\d{1,2}) ([A-Za-z]+) (\d{4})", v)
            if not m or m.group(2).lower() not in MONTHS:
                return None
            d, mo, y = int(m.group(1)), MONTHS[m.group(2).lower()], int(m.group(3))
    try:
        return date(y, mo, d).isoformat()
    except ValueError:
        return None


def shape(v):
    """Generalise a value to a format pattern: digits->9, letters->a/A."""
    return re.sub(r"[a-z]", "a", re.sub(r"[A-Z]", "A", re.sub(r"[0-9]", "9", v)))


# ---------------------------------------------------------------- 08 reference classifier
OUTCOMES = ["new", "unchanged", "duplicate", "invalid", "contradiction", "conflict"]
Valid = namedtuple("Valid", ["line", "vote", "session", "date", "title", "code", "ballot", "dkey"])


def validate(fields, header_len, check_membership=True):
    """08 rule 3, invalid step. Returns (Valid-like tuple without line, None) or (None, reason).
    With member_states missing, unknown-code and membership checks are skipped (counted by caller)."""
    if len(fields) != header_len:
        return None, "field count"
    f = [norm_text(x) for x in fields]
    vote, session, vdate, title, code, _name, ballot, _pub = f
    if not all([vote, session, vdate, title, code, ballot]):
        return None, "required field empty"
    code = code.upper()
    P = periods()
    if P is not None and code not in P:
        return None, "unknown country code"
    if P is None and not re.fullmatch(r"[A-Z]{3}", code):
        return None, "country code shape"
    if len(ballot) == 1:
        ballot = ballot.upper()
    if ballot not in ("Y", "N", "A", "X"):
        return None, "ballot"
    iso = parse_date(vdate)
    if iso is None:
        return None, "date"
    if not (session.isdigit() and int(session) > 0):
        return None, "session"
    if P is not None and check_membership and not member_on(code, iso):
        return None, "not a member on vote date"
    dkey = (int(session), iso, cmp_key(title))
    return (vote, int(session), iso, title, code, ballot, dkey), None


class Store:
    """Approved data plus pending conflicts, in memory. Rules 3-7 of 08."""

    def __init__(self):
        self.details = {}            # vote -> dkey (approved)
        self.ballot = {}             # (vote, code) -> ballot (approved)
        self.pend_details = defaultdict(set)   # vote -> {incoming dkey}
        self.pend_ballot = defaultdict(set)    # (vote, code) -> {incoming ballot}

    def import_rows(self, header, recs, label=""):
        """Classify one file. Returns (Counter outcomes, Counter reasons, extra dict)."""
        out, why, extra = Counter(), Counter(), Counter()
        hl = len(header)
        byvote = defaultdict(list)
        for r in recs:
            v, reason = validate(r.fields, hl)
            if v is None:
                out["invalid"] += 1
                why["invalid: " + reason] += 1
                continue
            byvote[v[0]].append(Valid(r.line, *v))
        for vote, rs in byvote.items():
            dkeys = {x.dkey for x in rs}
            if len(dkeys) > 1:                         # rule 4: whole vote held back
                out["contradiction"] += len(rs)
                why["contradiction: vote details"] += len(rs)
                extra["votes held back (details contradiction)"] += 1
                continue
            dkey = next(iter(dkeys))
            bycode = defaultdict(list)
            for x in rs:
                bycode[x.code].append(x)
            approved = self.details.get(vote)
            details_conflict = approved is not None and approved != dkey
            if details_conflict:
                if dkey in self.pend_details[vote]:
                    extra["details conflicts re-linked"] += 1
                else:
                    self.pend_details[vote].add(dkey)
                    extra["details conflicts opened"] += 1
            for code, xs in bycode.items():
                if len({x.ballot for x in xs}) > 1:
                    out["contradiction"] += len(xs)
                    why["contradiction: ballot"] += len(xs)
                    continue
                out["duplicate"] += len(xs) - 1
                x, key = xs[0], (vote, code)
                cur = self.ballot.get(key)
                if cur is None:
                    if details_conflict:                # rule 5: stored under approved details
                        out["conflict"] += 1
                        if member_on(code, approved[1]) if periods() is not None else True:
                            self.ballot[key] = x.ballot
                            extra["ballots stored under approved details"] += 1
                    else:
                        out["new"] += 1
                        self.ballot[key] = x.ballot
                        self.details.setdefault(vote, dkey)
                elif cur == x.ballot:
                    out["conflict" if details_conflict else "unchanged"] += 1
                else:
                    out["conflict"] += 1
                    if x.ballot in self.pend_ballot[key]:
                        extra["ballot conflicts re-linked"] += 1
                    else:
                        self.pend_ballot[key].add(x.ballot)
                        extra["ballot conflicts opened"] += 1
        return out, why, extra


def show_import(label, recs, res):
    out, why, extra = res
    total = sum(out.values())
    print(f"== {label}: " + " ".join(f"{o}={out.get(o, 0)}" for o in OUTCOMES)
          + f" | total={total} records={len(recs)}" + ("" if total == len(recs) else "  <-- MISMATCH"))
    for k, n in sorted(why.items()):
        print(f"     {k}: {n}")
    for k, n in sorted(extra.items()):
        print(f"     {k}: {n}")
