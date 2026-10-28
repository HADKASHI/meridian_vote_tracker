# Import classification and conflicts

Status: accepted

## Context

Assembly exports are messy (typos in codes, ballot words, mixed date formats, duplicates, rows that disagree), are re-sent with corrections, and sometimes silently drop votes. Every row needs exactly one outcome with a reason (ING-06), approved data must never be overwritten by a later file (ING-12, CON), and a reviewer should face as few decisions as the evidence allows.

## Decision

- **Files are evidence, row records are verdicts.** Each import keeps the original file with its SHA-256 and one record per line: line number, raw text, outcome, reason, normalisations, and links to the approved value or conflicts it produced. A verdict depends on the approved data at the moment of import, so it is stored, never recomputed.
- **Report, don't repair, except for documented normalisations**: trimming, invisible characters removed, codes and single-letter ballots upper-cased, day-first dates in several formats rewritten as ISO. Each is recorded on the row. Ballot words are never mapped (`Yes` is invalid), an empty ballot is never read as absent, and no date is guessed.
- **One outcome per row**, first match wins: invalid → contradiction → duplicate → conflict, unchanged or new. Country name and `published_at` are ignored when comparing; titles are compared ignoring case, spacing and typographic quotes and dashes, and stored as published.
- **A file that disagrees with itself about a vote's details holds back the whole vote.** Picking a majority would be the importer repairing the data.
- **A vote-details change is one conflict per vote**, not one per row; the ballot in each row is still compared on its own.
- **A conflict stores the incoming value and the revision it was compared against**; the current value is read live, so deciding one conflict never rewrites another. Unique indexes on (compared revision, incoming value) make a re-sent value link to the existing conflict, pending or decided, so re-uploads never flood the inbox and a rejected value never reopens.
- **Absence means nothing**: a missing vote or state changes nothing; an incomplete vote is a warning.
- **Every vote-details decision recalculates membership** for that vote, approving ballots that were invalid only because of membership, with the decision as their origin.
- The import is classified by a pure function over a vote's parsed rows plus its approved values, and written one vote per transaction under the vote's row lock, so an import and a decision on the same vote never interleave.

## Considered options

- **Mapping ballot words to letters**: `Yes` → `Y` would make the revision's later `A` for the same state a false conflict.
- **One conflict per row for a details change**: the Q1 revision would ask for 304 decisions instead of 8.
- **Copying the current value into the conflict**: deciding one conflict would then have to update others.

## Consequences

- The real Q1 files give predictable counts (original 5,737 new / 3 duplicate / 5 invalid / 151 contradiction; revision 8 conflicts), checked by tests that run when the data is present.
- Re-uploading the same file is safe: it is warned as identical and changes nothing.
