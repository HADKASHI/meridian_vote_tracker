# Snapshots store their answer; publication is a chain

Status: accepted

## Context

A published Alignment Index must read the same a year later, even after the data under it is corrected and the scoring code changes (SNP-04). Each reporting quarter has one official index, which the admin may need to replace openly (SNP-05). A snapshot must never be partial over the votes it names (POS-04).

## Decision

- A snapshot stores **its votes** (with the details and position revisions used), **a line per vote and member state** (the country vote revision used, or none, and its category: match, mismatch, abstain, absent, non-member, no data) and **results per state** (matched and counted weight as integers, rank or none), plus the scoring rules version. Nothing is recalculated when it is opened.
- Selection is one of a date range (inclusive, on approved dates), sessions or an explicit list. A **preview** returns the resolved vote count, the votes without a position, and warnings, with no job.
- **A vote without a position blocks the snapshot.** "Remove these votes" turns the selection into the rest, recorded as "range minus these votes". The job re-checks inside its transaction, because an import may add a vote after the preview.
- Pending conflicts, incomplete votes and a running import only **warn**, and each warning is kept on the snapshot as it was.
- The job reads its facts in one `REPEATABLE READ` transaction and writes everything in its final transaction.
- **Publication is an insert-only chain** per reporting quarter, chosen by the admin: snapshot, quarter, who, when, and for a replacement the publication it supersedes and a required reason. Partial unique indexes allow one first publication per quarter and one successor per publication, so of two simultaneous publications one wins and the other gets 409.

## Considered options

- **Store only the inputs and recompute the breakdown**: a change to the scoring code would rewrite old breakdowns.
- **Copy values instead of revision ids**: duplicates history that is already immutable.
- **Exclude votes without a position with a warning**: the index would quietly cover fewer votes than were chosen.
- **Derive the quarter from vote dates**: an index of Q1 votes may be published in April.

## Consequences

- A snapshot over all history is about 1M lines (about 70 MB); a quarter's is far smaller.
- Because a snapshot keeps the exact revisions it used, "what would differ if it were created now" is a read too: the stored revision ids against the current ones, and the saved selection against today's votes. It needs no job and shows no score.
- Comparing two snapshots is a pure read over stored results, lines, votes and the ballots they used. It can say why a state moved (the votes whose matched or counted weight differs) because every snapshot kept the revisions it used, and the reasons come in the same response as the movement.
- Unpublishing without a replacement is not offered.
