# 03: Accept or reject a conflict with a comment

**What to build:** in the decision pane an analyst accepts the incoming value or keeps the current one (reject), with an optional comment. The decision module locks the vote row first, records the decision with the revision it replaced (the current value at decision time, not the one the conflict was compared against), and on accept inserts a new revision whose origin is the decision and moves the pointer, all in one transaction. Rejecting changes nothing approved. Deciding one conflict never rewrites another. After the decision the pane shows it, the inbox reloads, and "Next conflict" moves to the next pending one.

**Blocked by:** 02

**Status:** done

- [x] `POST /api/conflicts/{conflictId}/decision` with accept makes the incoming value the approved value with the decision as its origin, and the previous revision stays in history (stories 53, 54)
- [x] Reject changes no approved value and no pointer
- [x] The decision records the replaced revision as the current one at decision time (story 55); another pending conflict on the same subject is unchanged and is then judged against the new current value
- [x] Works for both subjects: a ballot conflict and a vote-details conflict (CON-03); the vote row is locked first, so a decision never interleaves with an import of the same vote (story 64)
- [x] The response is the decision with from (current at decision time) → to (incoming) and the comment; a comment over 2,000 characters is a `validation` problem keyed `comment`; 404 for an unknown conflict
- [x] The pane offers Accept / Keep current with a comment field, shows the decision once made, and "Next conflict" goes to the next pending conflict in inbox order
- [x] Tests: API tests for accept and reject on both subjects, story 55, validation and 404; a Vitest test for deciding and Next conflict
