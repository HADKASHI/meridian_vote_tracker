# 03: Turn a sheet into checked rows

**What to build:** Given what a reader returned for a voting sheet (pages of cells with text and confidence), the system produces the vote header and one country vote per row, and a list of blocking and warning issues. Pure functions, written test-first, with synthetic fixtures shaped like the Assembly's sheets (the 2011/042 sheet with printed totals 70, 55, 7, 16 and its corrigendum with 69, 55, 8, 16).

**Blocked by:** None (can start immediately)

**Status:** done

**Requirements:** PDF-03, PDF-04

- [x] Code comes from the printed Code column; YES, NO and ABST. marks map to Y, N and A; a blank row maps to X
- [x] Header gives vote id, session, title and an ISO date from a long-form date such as 4 October 2011; pages of one sheet merge into one vote
- [x] Blocking: counts differ from any printed total; unknown code or not a member on the vote date (using the CSV import's own check); duplicate code; two marks or an unreadable cell on one row
- [x] Warning: low-confidence cell, header vote id differs from the file name, a member on the date has no row
- [x] The corrigendum fixture's one changed row (a state moving from YES to ABST.) and its totals are covered by named tests
- [x] Messages carry no document content beyond ids and codes
