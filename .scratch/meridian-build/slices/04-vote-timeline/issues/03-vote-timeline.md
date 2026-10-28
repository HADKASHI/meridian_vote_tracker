# 03: Vote timeline

**What to build:** on the vote page's timeline tab (`/votes/:voteId?tab=timeline`) an analyst reads the vote's whole story, newest first: imports that touched it with per-vote outcome counts, approved-value changes (vote details, country votes, positions and weights) with their origin, conflicts opened and decided, losing decision attempts, and snapshots and publications that used it. Events are ordered by history id, never by timestamp, and paged by history id ("Older" loads the next page), so a heavily revised vote still loads quickly. Served by `GET /api/votes/{voteId}/timeline` over the `vote_timeline` view (HIS-03, HIS-04, user stories 68 to 70; map ticket 04 rules 6 to 9).

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] The timeline returns every event kind of map ticket 04 rule 8, each with its actor, time and own fields (import with file and counts; revision with origin; conflict with subject and incoming value; decision with accept or reject and comment; not-applied attempt; snapshot with name; publication with quarter, reason and what it supersedes)
- [ ] Events are ordered by history id, newest first; `before` and `pageSize` page through them and `nextBefore` is null on the last page
- [ ] An unknown vote is a 404 ProblemDetails
- [ ] The timeline tab renders each event kind in glossary words and loads older pages on demand
- [ ] An integration test arranges a vote with every event kind using the fixture builders and reads it back in order across pages
