# 03: Say why a state moved between two snapshots

**What to build:** beside each state's rank and score movement, the compare response says why the score moved: the votes where the two snapshots treated the state differently. It is a pure read over what the two snapshots stored (their votes, positions, lines and the ballots they used), so it explains the numbers on the screen exactly and never recomputes. A reason is listed only when it changes what the state matched or counted; a vote that moved between abstain and absent changes no score and is not a reason. A state whose rank moved but whose score did not has no reasons of its own: its rank moved because other states' scores did.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] Each state carries a list of reasons, one per vote whose matched or counted weight differs between A and B, with the vote, whether it is in both snapshots or only one, and what changed on each side (category, ballot used, stance, weight)
- [x] A vote only in A, or only in B, is a reason when it counted for the state there; a vote that is in both and counted the same is not listed
- [x] The reasons add up: for every state the sum of matched and counted weight changes over its reasons equals the difference between the stored matched and counted weight in B and in A
- [x] A changed ballot (an accepted conflict between the snapshots), a changed position (stance or weight) and a different vote set are each proven by a named integration test through the HTTP API
- [x] A state n/a in one or both snapshots, and a state with no difference, return an empty list rather than null
- [x] It reads stored data only: later changes to approved data or positions do not change the reasons
- [x] The OpenAPI file is regenerated for the changed response only; no endpoint is added and no migration is needed
