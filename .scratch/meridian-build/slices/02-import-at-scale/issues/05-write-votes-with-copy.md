# 05: Write a vote's rows with COPY

**What to build:** The first `Performance` run missed the budget: 06:50 against 05:00, and 336 of those 410 seconds went to EF's `SaveChanges` of each vote's inserts (parse 8 s, lock 9 s, reading approved values 19 s, classifying 10 s, commit 17 s). The inserts of a vote's verdicts (row records, revisions, pointers, conflicts and their links) move to PostgreSQL binary COPY inside `ImportWriter`, still one vote per transaction under the same lock. Classification, the report and every slice 1 result stay exactly as they are.

**Blocked by:** 02 Performance test.

**Status:** ready-for-agent

- [ ] All slice 1 tests give the same results as before (including the brief's 7.2 example and crash resume)
- [ ] The `Performance` test finishes within 5 minutes with the same outcome counts
- [ ] Only the inside of `ImportWriter` changed; classification and its tests are untouched
