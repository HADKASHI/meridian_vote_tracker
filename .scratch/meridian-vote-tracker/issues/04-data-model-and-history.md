# Design the data model and history

Type: grilling
Status: resolved

## Question

_Scope narrowed while grilling: this ticket decides **what** the model must record. **How** history is stored moved to [Choose the history storage pattern](15-choose-history-pattern.md)._

How does the database represent votes, country votes, member states with membership periods, imports and their per-row outcomes, conflicts, positions and weights, so that HIS-01 to HIS-04 hold: every approved value knows its source, previous values are never lost, every decision and position or weight change records who, when, old and new, and a vote's full timeline can be shown? Options include append-only revision rows with a current pointer, a current table plus an audit log, or event sourcing. Constraints and indexes must protect integrity (brief section 9).

## Answer

Decided with the user over three grilling rounds. This settles **what** must be recorded; **how** is [Choose the history storage pattern](15-choose-history-pattern.md).

1. **Files are evidence, row records are verdicts.** Keep every uploaded file (SHA-256, uploader, upload time, `published_at`) plus one record per line of every file: line number, raw text, outcome, reason, and links to the approved value or conflict it produced. An outcome can't be recomputed later because it depends on the approved data at import time.
2. **Origin**: every approved value has exactly one origin, either the row that brought it in as new or the conflict decision that approved it. Positions are set by the admin directly.
3. **Only approved data is stored as approved.** Invalid and contradiction rows exist only as row records; a later clear value for them is *new*.
4. **Identity**: member states are keyed by the alpha-3 code; votes have a numeric id plus a unique `assembly_vote_id`; the published country name lives on the row record only. Membership periods must not overlap, checked in application code (no database constraint).
5. **Conflict record**: both sides with their origins, the subject (a country vote or vote details), status (pending, accepted or rejected), and the decision (who, when, accept or reject, optional comment). Decided conflicts stay viewable.
6. **Losing decision attempts are recorded** ("not applied, already decided by …") and shown on the timeline.
7. **Changes store only the new value.** "Previous" is the earlier entry in an append-only history ordered by sequence, not by timestamp. The API returns "from → to" pairs so every client agrees. This applies to positions and weights, country votes and vote details.
8. **Timeline** (HIS-04): imports touching the vote with per-vote counts, approved-value changes with origin, conflicts opened and decided (including losing attempts), position and weight changes, and snapshots that included the vote and their publication.
9. **Actor**: every recorded action names its user (Dana, Omar, Lena, or "system" for seeding).

Deferred to [Choose the history storage pattern](15-choose-history-pattern.md): database-level refusal of UPDATE and DELETE on history.
