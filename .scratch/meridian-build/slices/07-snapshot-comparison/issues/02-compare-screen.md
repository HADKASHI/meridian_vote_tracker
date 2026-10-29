# 02: Compare snapshots on a screen

**What to build:** at `/snapshots/compare` an analyst picks snapshot A and snapshot B from the calculated snapshots and sees a table of each member state's rank and score in both, with the movement between them. The two choices live in the query string, so a comparison can be linked and survives a reload. Scores show to one decimal and n/a is shown as n/a, never 0.

**Blocked by:** 01 Compare two snapshots' stored results

**Status:** ready-for-agent

- [ ] The page offers two pickers listing calculated snapshots by name; choosing both loads the comparison, and the choices are held in `?a=` and `?b=`
- [ ] The table shows per state: A rank and score, B rank and score, score change and rank change, with an up/down/no-change reading that does not rely on colour alone
- [ ] A state n/a or missing in one snapshot shows n/a and "no movement" for it
- [ ] Comparing a snapshot with itself, an unknown id and a failed load each show a clear message rather than an empty table
- [ ] Reached from the snapshots list and from a snapshot page; the feature folder imports only core, shared and the generated API
- [ ] Vitest specs with `HttpTestingController` cover the table, the n/a case and the query params
