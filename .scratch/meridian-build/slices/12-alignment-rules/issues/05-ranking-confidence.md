# 05: Metrics, a low-evidence flag and confidence filter and sort in the ranking

**What to build:** the snapshot page's ranking shows each state's completeness, abstention rate and missing data and its confidence band beside the score, with a "low evidence" flag on Low states. The analyst can filter the ranking by band and sort it by confidence, while rank numbers and the default order are unchanged.

**Blocked by:** 02 Evidence metrics and a confidence band on every result; 04 Choose the rule set in the new-snapshot form and show it everywhere

**Status:** done

- [x] Each ranking row shows the three metrics and the band beside the score and its basis; a state with no band shows none
- [x] Low states are flagged "low evidence"
- [x] The ranking can be filtered by band and sorted by confidence, and clearing them restores the stored order
- [x] Rank numbers never change with the filter or sort
- [x] Vitest specs cover the metrics, the flag, the filter, the sort and the unchanged rank
