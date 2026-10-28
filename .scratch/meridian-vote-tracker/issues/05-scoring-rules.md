# Decide the scoring rules

Type: grilling
Status: resolved

## Question

The brief deliberately leaves open how the Alignment Index treats abstentions (A), absences (X), votes held while a state was not a member, and a state with no countable vote (section 7.6). Decide each rule, how it is shown in a snapshot's per-country breakdown (SNP-03), and the rounding and tie-breaking of scores and ranks.

## Answer

Decided with the user in one grilling round. All rules are shown in each snapshot's per-country breakdown (SNP-03).

1. **Abstain (A)**: not counted (out of both numerator and denominator), as the brief's own formula states. Listed in the breakdown. Rejected: half credit, and counting it as non-alignment.
2. **Absent (X)**: not counted.
3. **Not a member on the vote date**: excluded, labelled "not a member".
4. **Member but no row for the vote**: not counted, labelled "no data", distinct from "absent" (a gap in what we received, such as vote 031 before rev2).
5. **Nothing countable**: score n/a (not 0), unranked, listed after the ranked states.
6. **Precision**: store matched weight and counted weight as exact integers per state; score = 100 × matched / counted; display 1 decimal; rank on the exact value.
7. **Ties**: exactly equal scores share a rank and the next rank is skipped (1, 2, 2, 4); within a tie, order by code.
8. **Thin evidence**: no minimum threshold; every score shows its basis (number of countable votes and counted weight).

Not decided here: votes with no position (POS-04), which belongs to [Design snapshots](11-snapshots.md).
