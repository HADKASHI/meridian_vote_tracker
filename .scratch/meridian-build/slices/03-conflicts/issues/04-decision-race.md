# 04: Exactly one of two simultaneous decisions applies

**What to build:** when Dana and Omar decide the same conflict at the same moment, exactly one decision applies. The other gets 409 `already-decided` with the winning decision in the body (who, when, accept or reject, from → to, comment), and their attempt is recorded as not applied, so it appears on the vote's timeline. A later decision on an already decided conflict is answered the same way. In the decision pane the 409 card replaces the decision buttons with the winner's decision and says the attempt was not applied and is on the vote's timeline; the inbox reloads and "Next conflict" still moves on.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] A decision on a decided conflict returns 409 `already-decided` whose `decision` is the winner's, and inserts a not-applied attempt with the loser's user and attempted decision (CON-05; stories 57, 58)
- [ ] The race test releases two decisions on one conflict together with the barrier, 20 rounds, and each round has exactly one 200 and one 409 `already-decided`, one decision record and one not-applied record for the loser
- [ ] The loser's attempt is listed in the conflict's not-applied attempts
- [ ] The UI shows the 409 card in place of the Accept / Keep current buttons, with the winner's decision and "your decision was not applied and is on the vote's timeline", and offers Next conflict
- [ ] Tests: the race test and a sequential 409 test through the API; a Vitest test with `HttpTestingController` that a 409 replaces the buttons with the card
