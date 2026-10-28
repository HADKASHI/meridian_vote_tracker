# The scoring rules

Status: accepted

## Context

The Alignment Index is a 0–100 score per member state measuring how often it voted with the Institute's positions, weighted (1–5). The brief gives the formula but leaves open how abstentions, absences, non-membership and missing data count, how precise scores are, and how ties rank. Each rule changes published numbers, so each must be explicit and recomputable by hand.

## Decision

- **Score = 100 × matched weight ÷ counted weight**, over the votes where the state voted yes or no while a member.
- **Abstentions and absences are not counted**, and neither is a vote outside the state's membership. A member with no row for a vote is **no data**, kept apart from **absent**, which the Assembly reported. Each is labelled in the breakdown.
- **Nothing countable means n/a**, not 0: no rank, listed after the ranked states.
- **Exactness**: matched and counted weights are stored as integers; the score is shown to 1 decimal and ranked on the exact value. The database checks that matched ≤ counted and that a rank exists exactly when something was counted.
- **Ties**: exactly equal scores share a rank and the next is skipped (1, 2, 2, 4); within a tie, states are listed by code.
- **No minimum threshold**: every score shows its basis, the number of countable votes and the counted weight.

## Considered options

- **Half credit for abstentions, or counting them as misses**: adds an editorial judgement the Institute didn't make.
- **A minimum number of votes before a state is ranked**: hides states rather than showing how thin the evidence is.
- **Floating-point scores**: rounding would make ties and recomputation by hand unreliable.

## Consequences

- Every published score can be recomputed from its breakdown.
- A snapshot records the scoring rules version, so a later change to these rules never alters an old snapshot.
