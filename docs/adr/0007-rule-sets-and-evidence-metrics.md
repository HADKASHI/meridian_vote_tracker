# Rule sets and evidence metrics

Status: accepted

## Context

ADR 0006 fixed one set of scoring rules and rejected half credit and a minimum-vote cutoff. The Institute now wants to see how an index changes under other treatments of abstentions and absences, and wants each score paired with how well the voting evidence supports it, so a ranking built on comprehensive evidence can be told from one skewed by missing data or heavy abstentions. A published index must still read the same a year later (ADR 0004).

## Decision

- **A rule set is a named, versioned and immutable set of scoring rules defined in code.** A snapshot stores its rule set id in `scoring_rules_version`, and a rule set is never edited once a snapshot has used it; a change is a new rule set. The default is `alignment-2026.1`, which is ADR 0006's rules unchanged, so every existing snapshot reads as before.
- **What a rule set decides is how abstentions and absences count**, and nothing else. A state outside its membership and a member with no data are never counted, under every rule set, because neither is something the state did.
  - `alignment-2026.1`: abstentions and absences excluded (the default).
  - `abstain-as-miss`: abstentions count as not aligned, absences excluded.
  - `abstain-half-credit`: abstentions earn half credit, absences excluded.
  - `strict`: abstentions and absences both count as not aligned.
- **Weights stay integers through a weight unit.** Each rule set declares a unit (1, or 2 for half credit); matched and counted weight are stored as integer points of 1 ÷ unit of a weight, so a half-credit abstention on a weight-1 vote is 1 of 2 points. The score is still 100 × matched ÷ counted, ranked on the exact value as fractions, and the screen divides by the unit to show weight. The database checks are unchanged.
- **Only the admin chooses a non-default rule set** when creating a snapshot. An analyst creates with the default. The snapshot page, the publications and the comparison show the rule set by name.
- **Evidence metrics are derived from the stored lines and never depend on the rule set**, so they describe the evidence and not the scoring. For each member state, over the votes where it was a member: **completeness** is yes and no ballots ÷ member votes, **abstention rate** is abstentions ÷ member votes, **missing data** is no-data votes ÷ member votes.
- **Confidence is a band from fixed thresholds** in one pure function: **High** at completeness of at least 90% and at least 10 countable votes; **Medium** at at least 70% and at least 5; otherwise **Low**. A state with nothing countable is n/a and has no band. The thresholds are the starting values and a change to them is recorded here.
- **Confidence never changes the rank.** The ranking shows the metrics, flags low evidence, and can be filtered or sorted by confidence.
- **Comparing snapshots scored under different rule sets** is allowed and warns, because the movement mixes a change of rules with a change of data. The comparison's reasons use each snapshot's own rule set.

## Considered options

- **Free-form rule parameters per snapshot:** every snapshot would carry its own unique rules, and "same rules" could no longer be said by name.
- **Rules edited by the admin in the database:** an edit would silently change what an old snapshot means, against ADR 0004.
- **A statistical confidence interval:** it can't be recomputed by hand, which every score must be able to.
- **Storing the metrics per result:** needs a migration for numbers the stored lines already give.
- **Letting confidence reorder the ranking:** hides states rather than showing how thin their evidence is, which ADR 0006 already rejected for thresholds.

## Consequences

- Supersedes the part of ADR 0006 that fixed abstentions and absences as always excluded and rejected half credit: those are now the default rule set's rules. Everything else in ADR 0006 holds.
- Any code that restates how a category counts (the comparison reasons) must ask the snapshot's rule set instead.
- The job refuses an unknown rule set and accepts any known one.
- Old snapshots get the metrics too, because they are derived from lines every snapshot already stores.
