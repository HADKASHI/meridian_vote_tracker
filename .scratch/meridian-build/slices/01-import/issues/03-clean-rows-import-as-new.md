# 03: Clean rows import as new

**What to build:** the tracer bullet through classification and writing. The handler groups the parsed rows by vote in memory, classifies each vote with a pure classification function (at first: every row of a well-formed vote is new), and persists one vote per transaction through `ImportWriter`, taking `SELECT … FOR UPDATE` on the vote row first. A vote row is created the first time its assembly vote id is seen; a new vote gets a details revision and its pointer; each new country vote gets a revision and pointer; every line gets a row record (line number, raw text, parsed fields, outcome, reason, links to the revision it created), with ids reserved from `history_seq` so a row and its revision point at each other. Progress reports rows read while parsing and votes done of total while applying. `GET /api/imports/{importId}` returns counts per outcome, `GET /api/imports/{importId}/rows` pages the row records (filterable by outcome and vote) and `GET /api/imports/{importId}/votes` gives counts per vote, all queried from the row records. The report page shows the counts as filters, the per-vote counts and the paged row table with links.

**Blocked by:** 02

**Status:** done

Requirements: ING-05, ING-06, ING-14, ING-15 (stories 11, 24, 34, 47, 48)

- [x] A clean fixture through the API: every row is `new`, approved values and details match the file with the row as origin, counts add up to the data rows
- [x] Rows of one vote that are not contiguous in the file are grouped into one vote
- [x] Rows are paged and filtered by outcome and vote; per-vote counts add up to the totals
- [x] The job ends `done` with votes done equal to votes total
- [x] The report page shows counts as filters, per-vote counts and the paged rows (Vitest)
