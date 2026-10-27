# Design snapshots

Type: grilling
Status: open
Blocked by: 04, 05, 15

## Question

How is a snapshot stored so it is immutable and reproduces exactly in a year (SNP-04) and answers "which data was the published index based on": frozen per-vote inputs (values, positions, weights) vs references to revision ids? How is it computed over ~1M country votes without blocking (SNP-02), selected by date range, session or list (SNP-01), and blocked or warned when a selected vote has no position (POS-04)? What does "one published snapshot per quarter" mean exactly: which quarter, and can a published one be replaced (SNP-05)?

## Comments

- From [Choose how data is read and written](09-data-access.md): calculation is decided. In one `REPEATABLE READ` transaction, a query streams facts (state, vote, ballot or none, membership, stance, weight) from the current views, a pure C# function applies the scoring rules, and inputs and results are written. Still open here: whether a snapshot may start while an import is part-way through (it would see the votes committed so far).

- From [Design background jobs and live progress](10-background-jobs.md): a snapshot is calculated as a job (queued, running, done, failed) in the `worker` container, alongside imports, one snapshot at a time, with a 10-min ceiling and progress polled every second. Still yours: whether a snapshot started during an import warns or blocks.
