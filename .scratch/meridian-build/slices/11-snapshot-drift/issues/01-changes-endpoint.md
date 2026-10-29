# 01: What changed in positions and vote details since a snapshot

**What to build:** an analyst asks what would differ if a calculated snapshot were created now and gets, for the votes it holds, the votes whose position changed and the votes whose vote details changed since it was calculated. Each kind has its real count and the first 20 votes, with the old and the new value, who or what changed it and when, and the time the answer was read. It is a pure read of the snapshot's stored revision ids against today's current ones: no job, no score, no rank, and the stored snapshot is untouched. Pending conflicts are not changes.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] `GET /api/snapshots/{snapshotId}/changes` returns, for position changed and details changed, the count and the first 20 votes (by assembly vote id), each with its old and new value
- [x] A position change shows old and new stance and weight and who set it; a details change shows old and new session, date and title and the origin (the import and its uploader, or the decision and who made it), with when
- [x] A vote whose position was set to the same value again is not listed; a vote with no change is not listed
- [x] The response carries the time it was read and no score or rank; no job is created and the snapshot's stored rows are unchanged
- [x] An unknown snapshot answers 404 `not-found`; a snapshot whose job has not finished answers 400 `bad-request`
- [x] Analysts and the admin may call it; nobody (401) is refused; the OpenAPI file is regenerated for this endpoint only
- [x] Integration tests through the HTTP API prove the above against fixture-built snapshots
