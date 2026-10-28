# 04: From → to pairs

**What to build:** every approved-value change on a vote's timeline reads as from → to, computed by the API so every client shows the same history. For a country vote revision, "from" is the earlier revision of the same member state in the same vote; for a vote details revision, the earlier details revision of the vote; for a position revision, the earlier position of the vote. The first revision of a subject has no "from". Pairs are correct even when the earlier revision sits on an older page. The pairing is a pure function over a subject's revisions in history-id order, unit-tested, and the timeline tab shows each pair (HIS-02, HIS-03, user story 71; map ticket 04 rule 7).

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] The pairing function gives each revision the previous revision of the same subject, and none to the first
- [ ] Country vote pairs never cross member states; details and position pairs never cross votes
- [ ] A revision whose previous revision is on an older page still carries its "from"
- [ ] The timeline tab shows "from → to" for country votes, vote details and positions
- [ ] Unit tests cover the pairing rules; an integration test checks pairs on a vote with an accepted ballot conflict, an accepted details conflict and a position change, across pages
