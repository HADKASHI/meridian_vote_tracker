# 06: Against approved data: unchanged and conflicts

**What to build:** map ticket 08 rules 5 to 7. The classification function also takes the vote's approved values and its existing conflicts. A row matching approved data is unchanged; a value with nothing approved yet is new and approved at once; a value that differs opens a conflict, leaving approved data untouched. Titles compare on their comparison key and are stored as published. A vote-details change opens one conflict per vote, and every row carrying the new details has outcome conflict and links to it; the ballot in each such row is still compared on its own, so a changed ballot opens its own ballot conflict (the row links to both) and a ballot with nothing approved is stored as new under the approved details, with membership checked against the approved date. A conflict stores the incoming value and the revision it was compared against. An incoming value equal to an existing conflict's (against the same revision) links to it instead of opening another; if that conflict was rejected, the row gets reason `previously_rejected`. A different value on the same subject opens its own conflict. The report lists the conflicts the import opened, with how many rows link to each.

The **brief's section 7.2 example** is the required integration test, through the API (upload → job → report). Its identifiers and values are illustrative:

Original file:

| Line | Vote | State | Ballot | Outcome | Why |
|---|---|---|---|---|---|
| 1 | 101 | BRA | Y | new | new vote, valid row |
| 2 | 101 | IND | Y | new | |
| 3 | 102 | JPN | Y | new | |
| 4 | 102 | JPN | Y | duplicate | identical to line 3 |
| 5 | 103 | FRA | Y | contradiction | France twice in one vote with different values |
| 6 | 103 | FRA | N | contradiction | see line 5; neither value is stored |
| 7 | 104 | FRN | X | invalid | country code does not exist |
| 8 | 104 | NZL | Y | invalid | New Zealand was not a member on the vote date |

Afterwards, BRA and IND are Y in 101 and JPN is Y in 102, approved; France has no value in 103 or 104.

Revised file:

| Line | Vote | State | Ballot | Outcome | Why |
|---|---|---|---|---|---|
| 1 | 101 | BRA | A | conflict | Y stays approved |
| 2 | 101 | IND | Y | see below | equal to the approved value |
| 3 | 103 | FRA | N | new | the earlier value was not stored; now one clear value |
| 4 | 104 | FRA | X | new | fills in a state missing from the vote |
| 5 | 105 | BRA | Y | new | new vote |
| — | 101 | | | conflict | the vote's title changed; a details change needs a decision too |
| — | 102 | | | no action | the vote is absent from the file; nothing is deleted |

Re-uploading the revised file gives 0 new values and 0 new conflicts.

**Line 2 under rule 5:** because vote 101's title changed, every row of 101 in the revised file carries the new details, so line 2's outcome is conflict (linked to the details conflict only), and its ballot matches the approved value, so no ballot conflict opens. The brief's table shows it as unchanged; the map's rule 5 (and the audited 304 conflict rows of rev2) decide conflict. The test asserts rule 5 and checks that IND's ballot opened no conflict.

**Blocked by:** 05

**Status:** ready-for-agent

Requirements: ING-09, ING-11, ING-12, CON-01, CON-03 (stories 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 44)

- [ ] Named domain tests for rules 5, 6 and 7, and for ignoring `country_name`, `published_at` and title formatting
- [ ] The 7.2 example through the API: outcome counts of both uploads, the approved values afterwards, the ballot conflict (BRA Y → A) and the details conflict on 101, and a second upload of the revised file opening no conflict
- [ ] A rejected conflict's value re-sent gets `previously_rejected` and opens nothing
- [ ] The report lists the conflicts opened with their row counts
