# 01: Vote search

**What to build:** an analyst opens `/votes` and finds any vote with approved details by assembly vote id or title (one search box, case-insensitive, partial match), by session, by a date range (inclusive, on the approved date) and by "no position". Results are paged and show the assembly vote id, session, date, title and position, each linking to the vote page by its internal id. Filters and the page live in query params, so a search survives a reload and can be shared. Served by `GET /api/votes` (HIS-01, user story 65).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `GET /api/votes` matches on assembly vote id or title, session, date range and `noPosition`, each alone and combined, and only votes with approved details are listed
- [ ] Results are ordered by approved date, newest first, then by assembly vote id, and paged with the total count; page size is clamped
- [ ] Each result carries the vote's current position (stance, weight, who set it) or none
- [ ] The `/votes` screen keeps its filters and page in query params and links each vote to `/votes/:voteId`
- [ ] Integration tests through the API arrange votes with the fixture builders and prove each filter
