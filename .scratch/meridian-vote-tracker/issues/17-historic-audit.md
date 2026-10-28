# Audit the historic file

Type: research
Status: resolved
Blocked by: 13

## Question

Run the [Audit the real data](07-data-audit.md) method (and its scripts under `research/07-data-audit-scripts/`, extended as needed) over `data_candidate/assembly_historic.csv` (962,171 rows, SHA-256 in [Bring in the historic file](13-historic-file.md)). Which of the 25 known defect classes occur, how often, and are there new ones? Does it overlap the Q1 exports (same vote ids, same or different values), and what would an import of it into an empty system and after the Q1 files produce under the rules in [Define import classification rules](08-import-classification.md)? Flag anything those rules or the schema don't yet handle, for [Lock the database schema](16-lock-database-schema.md). Also measure its shape for the 5-minute performance budget (rows per vote, votes per file) so the synthetic benchmark file can mirror it.

## Answer

Full report: [research/17-historic-audit.md](../research/17-historic-audit.md). Re-run with `bash research/17-historic-audit-scripts/run_all.sh`, which skips cleanly when a file is missing. member_states and both Q1 exports were uploaded again in this session and match 07's files byte for byte (same SHA-256), so nothing is pending.

- **Very clean**: 62 problem rows in 962,171. No BOM, LF endings, ISO dates only.
  - 30 exact duplicates
  - 11 truncated 7-field rows, all extra copies of good rows
  - 4 rows shifted by an unquoted comma in the title that still have 8 fields, all extra copies
  - 10 `XXX`/"Unknown" placeholder codes, each replacing a real member's row
  - 5 bad ballots (`0`, `?`, `YES`)
  - 2 votes cast before the state joined
  - **None**: contradictions, date problems, or vote ids off pattern
- **Predicted import into an empty system (08 rules)**: 962,109 new, 30 duplicate, 32 invalid, 0 contradiction. This leaves 15 incomplete votes, each missing one state. A re-import is all unchanged.
- **No overlap with Q1** (no shared vote ids, (vote, state) pairs or dates), so import order doesn't change any count.
- **Shape for the 5-minute benchmark**:
  - 98.8 MiB, 6,480 votes (720 a year, 12 per vote day)
  - 147–151 rows per vote, 150 member codes
  - fully interleaved by vote, so the whole file must be grouped before classifying
- **`published_at`** is set per vote (vote date + 2 days). Session is year − 1945 on every row.

**Correction to [Define import classification rules](08-import-classification.md)**: rule 1 names the required column `title`, but every real file uses `resolution_title`. Read it as `resolution_title`. Without that change, every real import fails.

Flags handed to [Lock the database schema](16-lock-database-schema.md) are listed in its Comments.
