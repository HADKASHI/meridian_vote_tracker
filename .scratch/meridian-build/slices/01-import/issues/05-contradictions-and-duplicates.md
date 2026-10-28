# 05: Contradictions and duplicates

**What to build:** map ticket 08 rules 3 and 4 in the classification function. Rules apply in order, first match wins: invalid → contradiction → duplicate → against approved data, and comparisons ignore `country_name` and `published_at`. Rows in one file that give one state different ballots in one vote are all contradictions, and none of their values is stored (`FRA Y, FRA Y, FRA N` are all contradictions). A vote whose valid rows disagree on session, date or title is held back entirely: every row of it is a contradiction, nothing is stored for it from this file, its vote row is still created, and the reason is reported once per vote. An exact repeat (same vote, code, ballot and details as an earlier row) is a duplicate while the first copy is classified normally. An invalid row never contradicts or duplicates a valid one.

**Blocked by:** 04

**Status:** done

Requirements: ING-08, ING-10 (stories 30, 31, 32)

- [x] Named domain tests for rule 3's order and for rule 4
- [x] Through the API, a contradictions fixture and a duplicates fixture give the expected outcomes, and nothing is approved for a held-back vote
