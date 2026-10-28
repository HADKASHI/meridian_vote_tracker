"""Predicted outcomes under the 08 rules (reference classifier in common.Store):
  A  empty -> historic -> historic again
  B  empty -> q1 original -> rev2            (cross-check against 08's hand-derived table)
  C  empty -> historic -> q1 original -> rev2
  D  empty -> q1 original -> rev2 -> historic
Steps whose file is missing print "pending: file missing" and are skipped. Without member_states the
unknown-code and membership checks fall back to a code-shape check and the result says so."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

if C.periods() is None:
    C.pending(C.MEMBERS, "unknown-code and membership checks")
    print("note: counts below treat any AAA code as a member, so XXX and non-member rows count as new")

cache = {}


def load(path):
    if path not in cache:
        cache[path] = C.rows(path)
    return cache[path]


def run(name, steps):
    print(f"\n######## scenario {name}")
    store = C.Store()
    for label, path in steps:
        if not C.have(path):
            C.pending(path, f"step '{label}' and the rest of scenario {name}")
            return
        header, rs = load(path)
        C.show_import(label, rs, store.import_rows(header, rs, label))
    print(f"   approved after scenario: {len(store.ballot)} ballots in {len(store.details)} votes; "
          f"pending conflicts: {sum(len(v) for v in store.pend_ballot.values())} ballot, "
          f"{sum(len(v) for v in store.pend_details.values())} details")
    if name.startswith("A") and C.periods() is not None:
        # incomplete votes after the historic import (08 rule 11 warning)
        from collections import defaultdict
        have = defaultdict(set)
        for (v, c) in store.ballot:
            have[v].add(c)
        inc = {v: sorted(C.members_on(store.details[v][1]) - have[v]) for v in store.details}
        inc = {v: m for v, m in inc.items() if m}
        print(f"   incomplete votes after import (rule 11 warning): {len(inc)}, missing slots "
              f"{sum(len(m) for m in inc.values())}")


H, O, R = ("historic", C.HIST), ("q1 original", C.ORIG), ("q1 rev2", C.REV2)
run("A: historic into empty system, then again", [H, ("historic again", C.HIST)])
run("B: q1 original -> rev2 (cross-check with 08)", [O, R])
run("C: historic -> q1 original -> rev2", [H, O, R])
run("D: q1 original -> rev2 -> historic", [O, R, H])
