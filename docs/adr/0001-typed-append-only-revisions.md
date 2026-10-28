# Typed append-only revisions with current pointers, guarded by the database

Status: accepted

## Context

The Alignment Index must be reproducible a year later, and analysts must answer "which file, who uploaded it, who approved it" for any value. Data arrives as files that are re-sent with corrections, and conflicting values are settled by people. Nothing approved may be lost or silently rewritten (HIS-01 to HIS-04, SNP-04).

## Decision

- Country votes, vote details and positions each keep an **insert-only revision history**. Every revision has exactly one **origin**: the import row that brought it in as new, or the conflict decision that approved it (a position records who set it).
- Two small **pointer tables** (`vote_current`, `country_vote_current`) say which revision is approved now. Only the pointers, and the `job` queue, ever change.
- A **guard trigger** refuses UPDATE, DELETE and TRUNCATE on every other table, reference tables included. It runs per statement, so even a statement that matches no row is refused. A test finds every table from the catalog and proves the guard on each, so a new table can't escape it.
- One shared sequence, `history_seq`, supplies the id of every guarded table, so a vote's timeline orders by id across tables. Per vote this order is exact, because everything that touches a vote takes its row lock first.
- A conflict decision is its own insert-only record, unique per conflict; the database rejects a second one, which is how exactly one of two simultaneous decisions wins.
- "Previous" is always the earlier revision of the same subject, and the API builds from → to pairs.

## Considered options

- **Current tables plus a JSON audit log** written by EF's SaveChanges hook: the bulk import path bypasses the hook, and the history is untyped.
- **Event sourcing**: the right shape for some of this, but too heavy for the time available, and it sidelines EF Core, which the brief asks for.
- **Triggers copying old rows**: they record row diffs, not decisions or origins.
- **Temporal tables**: EF supports them only on SQL Server; PostgreSQL 18 has no system versioning.

## Consequences

- A snapshot stores revision ids, so later corrections can't change it.
- The race-sensitive code is one locked pointer move or one unique insert.
- Tests can't reset with TRUNCATE; each test copies a migrated template database instead.
- The guard protects against mistakes, not against the database owner. Running the app as a non-owner role is a before-production item.
- A row record and the revision it created point at each other, so those two keys are checked at commit, with ids reserved from the sequence first.
