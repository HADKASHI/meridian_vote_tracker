# 03: Votes that now match, and votes that no longer match, the selection

**What to build:** the changes answer re-resolves the snapshot's saved selection (including its removed votes) against today's votes and lists the votes now matching it that the snapshot does not hold, and the votes the snapshot holds that no longer match it (for example a vote whose date was corrected out of the selected range). Each kind has its real count and the first 20 votes with the vote's current details and, for a vote that left the selection, why it left (its old and new details) and who or what changed it.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Votes now selected but not in the snapshot: a new vote in the selected date range is listed; a vote the snapshot removed ("range minus these votes") is not
- [ ] A selected vote that still has no position is listed with its position shown as none
- [ ] Votes no longer selected: a held vote moved out of the date range or session is listed with its old and new details and origin; a held vote still matching is not
- [ ] Selection by date range, by sessions and by an explicit list are each resolved the way the preview resolves them
- [ ] Both kinds give the real count and the first 20 votes
- [ ] Integration tests through the HTTP API prove the above against fixture-built snapshots
