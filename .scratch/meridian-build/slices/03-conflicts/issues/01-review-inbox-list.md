# 01: Review inbox lists conflicts

**What to build:** an analyst opens `/review` and sees the Review inbox: a split pane whose list shows every pending conflict with its vote, subject (ballot with its member state, or vote details), current → incoming and the import that opened it. A vote's conflicts sit next to each other (ordered by vote, then subject: details first, then ballots by state code). The current value is read live, so a conflict opened before another decision shows today's approved value. A filter in the query params switches between pending, decided and all, and the list can be narrowed to some votes or one import. Each list item carries a `kind` (`conflict`) so other inbox item kinds can join later. Data is arranged with slice 0's fixture builders.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `GET /api/conflicts` returns pending conflicts by default, paged, ordered by vote then subject, each with vote ref, subject, state code, live current value, incoming value, opening import and opened-at time (stories 50, 64)
- [ ] `state=decided` and `state=all` include decided conflicts with their decision; `voteIds` and `importId` narrow the list
- [ ] The current value shown is the approved value now, not the one the conflict was compared against
- [ ] `/review` lists the conflicts in a split pane, with the pending/decided filter kept in the query params, and links each item to `/review/:conflictId`
- [ ] Tests: API tests for ordering, filters, paging and the live current value; a Vitest test for the list
