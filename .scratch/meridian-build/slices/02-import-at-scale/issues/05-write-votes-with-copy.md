# 05: Write a vote's rows with COPY

**What to build:** The first `Performance` run missed the budget: 06:50 against 05:00, and 336 of those 410 seconds went to EF's `SaveChanges` of each vote's inserts (parse 8 s, lock 9 s, reading approved values 19 s, classifying 10 s, commit 17 s). The inserts of a vote's verdicts (row records, revisions, pointers, conflicts and their links) move to PostgreSQL binary COPY inside `ImportWriter`, still one vote per transaction under the same lock. Classification, the report and every slice 1 result stay exactly as they are.

**Blocked by:** 02 Performance test.

**Status:** wontfix

- [ ] All slice 1 tests give the same results as before (including the brief's 7.2 example and crash resume)
- [ ] The `Performance` test finishes within 5 minutes with the same outcome counts
- [ ] Only the inside of `ImportWriter` changed; classification and its tests are untouched

## Comments

- One test line changed: the crash-resume test named EF's `DbUpdateException` in the job's redacted error; the same database failure now surfaces as `PostgresException`. The progress it asserts (2 of 4 votes kept) is unchanged.
- Built, measured (03:02 against 06:50, 732 MiB growth against 1,471 MiB) and passing the slice 1 tests, then taken out again on the user's decision (2026-10-29) to keep the EF writer and the existing 200 MB upload limit. The commit "Write a vote's rows with binary COPY" in pull request #12 holds it. See ADR 5.
