# 02: Admin sets a position

**What to build:** the admin sets a vote's stance (yes/no) and weight (1–5) inline on `/positions`. `PUT /api/votes/{voteId}/position` takes the vote's lock, inserts a position revision recording who set it and when, and moves the vote's pointer only if it still points at the revision the admin saw (the revision id is the version). If someone changed it in the meantime, the save is refused with 409 `stale-position` carrying the current position. Analysts see the controls disabled with "Admin only".

**Blocked by:** 01

**Status:** done

- [x] The admin sets a first position (seen revision null) and changes an existing one; the response is the new position and the worklist shows it
- [x] Every save adds a revision; earlier revisions stay in history
- [x] An analyst gets 403 `forbidden` and nobody gets 401 (role matrix)
- [x] A missing or out-of-range stance or weight gets a 400 validation problem keyed by the field
- [x] An unknown vote gets 404 `not-found`
- [x] A save whose seen revision is no longer current gets 409 `stale-position` with the current position, and nothing changes
- [x] Race test: two saves with the same seen revision, released together, for 20 rounds: exactly one 200 and one 409 each round
- [x] On `/positions` the admin edits stance and weight inline and saves; analysts see the controls disabled with "Admin only"
