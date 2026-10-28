# Design snapshots

Type: grilling
Status: resolved
Blocked by: 04, 05, 15

## Question

How is a snapshot stored so it is immutable and reproduces exactly in a year (SNP-04) and answers "which data was the published index based on": frozen per-vote inputs (values, positions, weights) vs references to revision ids? How is it computed over ~1M country votes without blocking (SNP-02), selected by date range, session or list (SNP-01), and blocked or warned when a selected vote has no position (POS-04)? What does "one published snapshot per quarter" mean exactly: which quarter, and can a published one be replaced (SNP-05)?

## Comments

- From [Choose how data is read and written](09-data-access.md): calculation is decided. In one `REPEATABLE READ` transaction, a query streams facts (state, vote, ballot or none, membership, stance, weight) from the current views, a pure C# function applies the scoring rules, and inputs and results are written. Still open here: whether a snapshot may start while an import is part-way through (it would see the votes committed so far).

- From [Design background jobs and live progress](10-background-jobs.md): a snapshot is calculated as a job (queued, running, done, failed) in the `worker` container, alongside imports, one snapshot at a time, with a 10-min ceiling and progress polled every second. Still yours: whether a snapshot started during an import warns or blocks.

## Answer

Decided with the user in two grilling rounds. The user overturned one proposal: votes with no position were to be warned about and excluded (POS-04); instead they **block** the snapshot, so no snapshot is ever partial over the votes it names.

1. **Storage (SNP-04, SNP-03)**: a snapshot keeps three things, all insert-only and covered by the history guard once calculated:
   - its **votes**: each selected vote with the vote details revision and position revision used;
   - its **lines**: one per (vote, member state), holding the country vote revision used (null = no data) and the **category** decided at calculation time (match, mismatch, abstain, absent, non-member, no data);
   - its **results**: per state, matched and counted weight (exact integers) and rank (null = n/a).
   The header records the scoring rules version. Nothing is recomputed on open, so neither later data nor later code can change an old snapshot or its breakdown. A snapshot over all history is ~1M lines (~70 MB); a quarter's is far smaller. Rejected: inputs-only with a recomputed breakdown (scoring code changes would rewrite old breakdowns); copying values instead of revision ids (duplicates history that is already immutable).
2. **Lifecycle**: the snapshot header (name, note, selection, who, when) is inserted when requested, and its job links to it. The job writes votes, lines, results and warnings in its single final transaction. A failed job leaves a header with no results, shown as failed.
3. **Selection (SNP-01)**: one mode per snapshot: a date range (inclusive, on each vote's approved date at calculation time), one or more sessions, or an explicit list. The criteria are stored as entered; the resolved vote list (item 1) is the truth. A selection that resolves to no votes is refused up front (400, "no votes match").
4. **No position blocks (POS-04)**: the preview returns votes without a position as an error, listing them, and Calculate is disabled. "Remove these votes" turns the selection into an explicit list of the rest; the stored criteria keep the original range or sessions and the removed votes ("2024-01-01 → 2024-03-31, minus MA/2024/007, 012, 019"). The create endpoint refuses (400) a selection that currently contains such a vote, and the job re-checks inside its REPEATABLE READ transaction, failing with the offending vote and why ("MA/2024/041 has no position, added by import #13 after the preview"). Positions can't be removed, so the reverse race can't happen.
5. **Preview**: `POST /snapshots/preview` is a cheap count query with no job. It returns the resolved vote count, the blocking error of item 4, and warnings: pending conflicts on selected votes (the snapshot uses approved values), incomplete votes, and an import currently running. Warnings never block. The job stores each warning on the snapshot as it stood at calculation time, including "calculated while import #12 was applying (812 of 1,204 votes)", which is what the REPEATABLE READ actually saw. This settles the question from [Design background jobs and live progress](10-background-jobs.md): a snapshot during an import **warns, never blocks**.
6. **Quarter (SNP-05)**: the reporting quarter is chosen by the admin when publishing (e.g. 2024-Q1), defaulting to the current calendar quarter. It is not derived from vote dates, since an index of Q1 votes may be published in April.
7. **Replacing a publication**: publication is an insert-only record (snapshot, quarter, who, when, reason, and the publication it supersedes). The admin may replace the quarter's published snapshot with another, giving a required reason; the earlier one stays visible as "superseded by …, reason …". The database allows one unsuperseded publication per quarter and one successor per publication; of two admins publishing at once, one wins and the other gets 409 with the current state. Unpublishing without a replacement is not offered.
8. **Name**: a required name (not unique: retries and variants are normal) and an optional note, both fixed at creation. The list shows name, creator, time, vote count and publication badge.
9. **Comparing two snapshots** (rank movement between quarters, or a publication and its replacement) is not part of this design; it is a candidate extra for [Set the build order](12-build-order.md), a pure read over item 1's lines.
