# 01: Score a snapshot by the scoring rules

**What to build:** the Alignment Index is calculated by one pure function with no database access. Given the facts of a snapshot (for each selected vote its stance and weight, and for each member state whether it was a member on the vote date and its approved country vote, if any), it returns a categorised line per vote and member state (match, mismatch, abstain, absent, non-member, no data), and per member state the matched and counted weight as exact integers, the number of countable votes and a rank. It carries a scoring rules version that a snapshot records. Every numbered rule of map ticket 05 is proven by its own named domain test.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Rule 1: an abstention is labelled abstain and counts in neither matched nor counted weight
- [ ] Rule 2: an absence is labelled absent and is not counted
- [ ] Rule 3: a vote held while the state was not a member is labelled non-member and is not counted, whatever ballot is stored
- [ ] Rule 4: a member with no country vote is labelled no data, kept apart from absent, and is not counted
- [ ] Rule 5: a state with nothing countable has no score and no rank (n/a), and is listed after every ranked state
- [ ] Rule 6: matched and counted weight are exact integers; the score is 100 × matched ÷ counted and ranks on the exact value (no rounding before ranking)
- [ ] Rule 7: exactly equal scores share a rank and the next is skipped (1, 2, 2, 4); within a tie, and among n/a states, states are ordered by code
- [ ] Rule 8: no minimum threshold; a state with one countable vote is ranked, and every result carries its basis (countable votes and counted weight)
- [ ] A yes or no ballot matching the stance is a match and adds its weight to both; a differing one is a mismatch and adds it to counted only
- [ ] The scoring rules version is a fixed value exposed by the function's module
