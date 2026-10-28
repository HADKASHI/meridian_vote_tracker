# 05: Vote-details decisions recalculate membership

**What to build:** every vote-details decision recalculates membership for its vote against the approved date after the decision (map ticket 08 rule 8; stories 60–62). Ballots from rows of that vote that were invalid only because the state was not a member on the vote date, and that pass now, are approved with the decision as their origin; their row records keep the verdict given at import. When several such rows give one state different ballots, the earliest row (upload order) is approved and each later different ballot opens a ballot conflict against it, linked to its row, so nothing is dropped silently. Approved ballots now outside membership stay stored but stop counting. The decision reports both effects, and the decision pane shows them.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] Accepting a date correction approves the ballot of each row invalid only for membership whose state is a member on the new date, with the decision as origin, unless the state already has an approved ballot
- [ ] Rows for one state with different ballots: the earliest is approved and each later distinct ballot opens one ballot conflict against that revision, linked to its row
- [ ] Approved ballots of states not members on the new date stay approved (still the current revision) and are reported as no longer counted
- [ ] A rejected details decision recalculates too, and with the date unchanged nothing changes
- [ ] The decision and the decided conflict report the ballots approved and the states no longer counted; the pane shows them
- [ ] Membership is inclusive at both ends and an empty end date means still a member, with a named test for the boundary day
- [ ] Tests: API tests for approving, the earliest-wins rule, stopping counting, reject, and the boundary day; a Vitest test for the pane showing the effect
