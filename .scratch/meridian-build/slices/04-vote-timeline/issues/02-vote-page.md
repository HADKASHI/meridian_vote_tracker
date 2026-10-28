# 02: Vote page with origins

**What to build:** an analyst opens `/votes/:voteId` and sees the vote's current state: its approved details (session, date, title), its position and weight (read-only; editing is slice 5's), the number of pending conflicts, and every country vote. The country votes list every state that was a member on the vote date plus any state with an approved ballot, each with its ballot or none and whether it was a member on the date. Every approved value names its origin: the import row that brought it in (file, line, uploader, upload time) or the conflict decision that approved it (who, when, which conflict). A vote seen only in a contradiction shows no details. Served by `GET /api/votes/{voteId}` (HIS-01, HIS-02, user stories 66 and 67).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `GET /api/votes/{voteId}` returns the approved details, position, country votes and pending conflict count; an unknown id is a 404 ProblemDetails
- [ ] A value brought in by a file names its import, file name, line, row, uploader and upload time
- [ ] A value approved by an accepted conflict names the decision, its conflict, who decided it and when
- [ ] Member states with no approved ballot on the vote date are listed with no ballot; a state with a ballot that was not a member on the date is listed as not a member
- [ ] The vote page shows the details, position and country votes with each origin, and links an import origin to its import
- [ ] Integration tests through the API cover both origin kinds, membership and the 404
