# Audit the real data

Type: research
Status: resolved
Blocked by: 01

## Question

Inspect every file in `candidate_data/` and list each defect class it contains, with counts and example row numbers: malformed CSV, wrong column counts, missing fields, unknown or lower-case country codes, invalid ballots, date formats, non-member votes, in-file duplicates and contradictions, inconsistent vote details, name variants for one code (Turkey/Türkiye), split votes, and what rev2 changes relative to the Q1 file. Note the brief's date-format inconsistency (ING-04 says dd-mm-yyyy, Appendix A says yyyy-mm-dd) and what the files actually use.

## Answer

Full catalogue (25 defect classes, each with counts, line numbers, requirement and expected outcome), predicted import counts and re-runnable scripts: [research/07-data-audit.md](../research/07-data-audit.md), scripts in `research/07-data-audit-scripts/` (`bash run_all.sh`). Headline counts were spot-checked independently.

- **Dates**: both files use `yyyy-mm-dd` (as Appendix A says) except 6 rows of vote 027 written `21/02/2024`. No row uses ING-04's `dd-mm-yyyy`, so the brief's own contradiction resolves in favour of Appendix A; the decision belongs to [Define import classification rules](08-import-classification.md).
- **Original file, row defects (one each)**: a 5-field row with `;` (line 3139), unknown code `FRN` (typo), lower-case `deu`, ballot `Yes`, empty ballot, NZL voting in 2024 after leaving in 2021.
- **Original file, vote defects**: 3 exact duplicates (vote 007); FRA both Y and N (vote 012); 2 rows with a different title (vote 025); 6 rows with a slash date (vote 027); vote 031 truncated at 80 of 149 states.
- **rev2**: no BOM, CRLF, rows shuffled; TUR published as "Turkey" not "Türkiye" (ING-11). Against the original: 6 ballot changes, 2 vote-detail changes (vote 009 title, vote 014 date), 72 states added (69 complete vote 031), new vote 041, and **vote 040 missing entirely** (ING-09: absence is not a correction).
- **Trap**: the 3 original values that never became approved data (ARG `Yes`, AZE empty, FRA Y/N) all arrive as `A` in rev2. They must be `new`, not conflicts; normalising `Yes` to `Y` on the first import would create a false conflict.
- **Predicted counts** (baseline assumptions in the report): original 5,878 new / 3 duplicate / 11 invalid / 4 contradiction; rev2 231 new / 5,425 unchanged / 304 conflict rows (6 ballot + 2 vote-detail conflicts); rev2 again 0 new and 0 new conflicts.
- **PDFs**: image-only scans, no text layer; the 2007 sheet has hand corrections, a double mark and a non-member code (ANT). Session numbers don't follow the year, so session is not validated against the date.
- **Not yet audited**: `assembly_historic.csv`, blocked on [Bring in the historic file](13-historic-file.md).

Open questions handed to [Define import classification rules](08-import-classification.md): which date formats to accept; whether an in-vote title or date mismatch makes only the odd rows or the whole vote a contradiction; whether the first copy of a duplicate is "new"; whether a vote-detail change is one conflict or one per row; how re-importing behaves while conflicts are pending; whether approved votes absent from a later file are reported.
