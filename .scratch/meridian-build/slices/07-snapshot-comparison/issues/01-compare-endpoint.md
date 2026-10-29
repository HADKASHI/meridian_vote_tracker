# 01: Compare two snapshots' stored results

**What to build:** an analyst asks for two calculated snapshots, A and B, and gets every member state's rank and score in each, with the movement between them. It is a pure read over the results the two snapshots stored: nothing is recalculated, so the answer is the same after the underlying data or the scoring code changes. A state that is n/a (nothing countable) in one snapshot, or missing from it, shows no score or rank there and no movement, instead of a made-up zero.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Each state is returned with its score and rank in A and in B, to the stored precision, with score change (B minus A) and rank change (A's rank minus B's, so a positive number means it moved up)
- [ ] A state n/a in one snapshot (or absent from it) has null score and rank there and null changes; a state n/a in both is still listed
- [ ] States are ordered by rank in B, then those with no rank in B, ties and unranked ordered by code
- [ ] It reads stored results only: changing approved data, positions or the scoring after both snapshots are saved does not change the comparison
- [ ] An unknown snapshot id for A or B answers 404 `not-found`; a snapshot whose job has not finished answers 400 `bad-request`
- [ ] Analysts and the admin may call it; nobody (401) is refused, and the OpenAPI file is regenerated for the changed response only
- [ ] Integration tests through the HTTP API prove the above against fixture-built snapshots
