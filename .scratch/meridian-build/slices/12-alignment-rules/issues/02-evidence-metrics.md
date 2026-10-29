# 02: Evidence metrics and a confidence band on every result

**What to build:** every state's result carries how well the voting evidence supports its score: completeness (yes and no ballots ÷ votes in which it was a member), abstention rate, missing data (no-data votes ÷ member votes) and a confidence band. They are derived from the snapshot's stored lines, never from the rule set, so they are the same under every rule set and old snapshots have them too. Confidence is High at completeness of at least 90% and at least 10 countable votes, Medium at at least 70% and at least 5, otherwise Low; a state with nothing countable has no band. Confidence never changes the rank.

**Blocked by:** 01 Create a snapshot under a chosen rule set

**Status:** ready-for-agent

- [ ] One pure function derives completeness, abstention rate, missing data and the band from a state's line counts, with the thresholds in one place
- [ ] Domain unit tests name each band and the boundaries (90% and 70% exactly, 10 and 5 countable votes exactly), and a state with nothing countable has no band
- [ ] The snapshot's results and a state's breakdown result carry the three metrics and the band, derived from the stored lines
- [ ] The metrics and band are identical for the same data scored under each rule set
- [ ] A snapshot stored before this change returns the metrics too, and its scores, weights and ranks are unchanged
- [ ] Rank is unchanged by the band: a Low state can outrank a High one
- [ ] Integration tests through the HTTP API prove a High, a Medium, a Low and a no-band state over fixture data
- [ ] The OpenAPI file is regenerated for these endpoints only; no migration is added
