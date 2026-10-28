# 01: Positions worklist

**What to build:** anyone can open `/positions` and see every vote with its approved details, stance and weight, votes without a position first, so the admin sees what still needs a position. `GET /api/positions` serves the worklist paged, with a "no position" filter. The page is read-only in this ticket.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Votes without a position come before votes with one; within each group, ordering is stable (vote date, then assembly vote id)
- [ ] `noPosition=true` returns only votes without a position, and the total counts only those
- [ ] Paging returns the requested page with the full total; page and page size outside their limits get a 400 validation problem
- [ ] Each item carries the vote reference, session, date, title and its position (revision id, stance, weight, who set it, when) or null
- [ ] Analysts and the admin can both read it
- [ ] `/positions` lists the worklist with a "No position" filter kept in the query params, and pages through it
