# Define import classification rules

Type: grilling
Status: resolved
Blocked by: 04, 07, 15

## Question

What exact algorithm gives each row exactly one outcome of new, unchanged, duplicate, invalid, contradiction or conflict (ING-03 to ING-13)? Cover: grouping rows by vote when they are not contiguous; which normalisations are allowed and how they are reported (ING-13); vote-detail conflicts (date, title, session; CON-03); idempotent re-upload of the same or an overlapping file without re-opening a pending conflict (ING-12); split votes (ING-10); whether published_at ordering matters; and the shape of the persisted import report (ING-14, ING-15).

## Answer

Decided with the user over four grilling rounds, using the defect catalogue in [Audit the real data](07-data-audit.md) and the model from [Design the data model and history](04-data-model-and-history.md) and [Choose the history storage pattern](15-choose-history-pattern.md).

1. **Parsing.** Header-driven: columns matched by name (case-insensitive, trimmed), any order. Extra columns are ignored and noted as a file warning. Required: `assembly_vote_id`, `session`, `vote_date`, `title`, `country_code`, `ballot`; optional and stored when present: `country_name`, `published_at`. BOM stripped; LF and CRLF accepted. A row whose field count differs from the header is invalid. A missing required column, invalid UTF-8, or no data rows records the import as **failed** (file, SHA-256, uploader kept), with no row records and nothing applied.
2. **Normalisations (ING-13)**, each recorded on the row record and counted in the report:
   - stored value: trimmed, Unicode NFC, control and invisible characters removed (zero-width, NBSP to space, stray `\r`); country codes and single-letter ballots upper-cased; dates converted to ISO from `yyyy-mm-dd`, `yyyy/mm/dd`, `dd-mm-yyyy`, `dd/mm/yyyy`, `dd.mm.yyyy` (leading zeros optional) and the long form `14 March 2007`. Numeric dates are always read day-first.
   - comparison key (for duplicate, contradiction, unchanged and conflict): the stored value, case-folded, whitespace runs collapsed, typographic quotes and dashes unified. Accents are kept. Stored titles keep their case.
   - never: mapping ballot words (`Yes`, `no`) to letters, treating an empty ballot as `X`, guessing codes (`FRN`), month-first dates, 2-digit years, impossible dates, dates with a time part. These are invalid. Mapping `Yes` to `Y` would create a false ARG conflict in rev2.
3. **One outcome per row**, first match wins: **invalid** (structure, required field, code, ballot, date, session not a positive integer, not a member on the vote date) → **contradiction** → **duplicate** (same vote, code, ballot and details as an earlier row; the first copy goes on to be classified normally) → **conflict**, **unchanged** or **new** against approved data. Comparisons ignore `country_name` (ING-11) and `published_at`. So `FRA Y, FRA Y, FRA N` are all contradiction, and an invalid row never contradicts or duplicates a valid one.
4. **Contradiction in vote details** (session, date or title disagreeing within one file): the **whole vote** is held back. Every row of it is a contradiction, nothing is stored for it from this file, and the reason is reported once per vote. The importer never picks a majority value.
5. **Vote-details conflicts (CON-03)**: one conflict per vote, whose subject is the vote details; every row carrying the new details has outcome conflict and links to it. The ballot in each such row is still compared on its own: a changed ballot opens its own ballot conflict (the row links to both), and a ballot with no approved value is stored as new under the approved details, with membership checked against the approved date. Accepting the conflict changes the details only.
6. **Conflict record**: a copy of the incoming value plus a link to the revision it was compared against when it opened. "Current" is never copied; it is read live through the current pointer, so deciding one conflict never updates another. A decision names the revision it replaced and the one it created.
7. **Re-import while pending (ING-12)**: an incoming value equal to a pending conflict's incoming value links to that conflict (outcome conflict, nothing new opened). A value differing from the approved value and from every pending value opens another conflict on the same subject; each is decided by a person against the current value at decision time. A value equal to the approved value is unchanged, even while a conflict is pending.
8. **Every vote-details decision recalculates membership** for that vote. Approved ballots now outside membership stay stored but stop counting (scoring already uses the vote's current approved date). Rows of that vote that were invalid **only** because of membership and now pass have their ballot approved with the decision as origin; their row records keep the verdict given at import, and the timeline links the two.
9. **Same file again** (same SHA-256): imported normally with a warning "identical to <import>". Classification is idempotent, so the report shows nothing changed.
10. **`published_at`** is stored and shown, never used for ordering; classification follows upload order. Warning when it is earlier than the newest `published_at` already imported.
11. **Absence (ING-09, ING-10)** never changes approved data and gets no outcome. Warning per **incomplete vote**: members on the vote date with no approved ballot after this import. Approved votes missing from a file are not listed.
12. **Membership**: `member_from` and `member_to` inclusive; empty `member_to` means still a member. **Session**: positive integer, consistent within a vote (rule 4), never checked against the year.
13. **Report (ING-14, ING-15)**: row records are the source of truth: line number, raw text, outcome, reason code plus readable message, normalisations applied, links to the approved value or conflicts. Counts per outcome and per vote are queried from them, never stored. The import stores only file-level facts: status, BOM, line endings, and the warnings from rules 1, 9, 10 and 11.
14. **Grouping (D-21)**: row records are insert-only, so a row's outcome must be known before it is written. The whole file is parsed and grouped by vote id in memory, then classified and written one vote per transaction under the per-vote lock. Streaming for the ~1M-row historic file is for [Design background jobs and live progress](10-background-jobs.md).

**Predicted counts**, derived by hand from the audit's baseline; re-run `research/07-data-audit-scripts/05_simulate.py` with these rules once `data_candidate/` is back:

| Import | new | unchanged | duplicate | invalid | contradiction | conflict rows | conflicts opened |
| --- | --- | --- | --- | --- | --- | --- | --- |
| original | 5,737 | 0 | 3 | 5 | 151 | 0 | 0 |
| rev2 | 372 | 5,284 | 0 | 0 | 0 | 304 | 8 (6 ballot, 2 details) |
| rev2 again | 0 | 5,656 | 0 | 0 | 0 | 304 | 0 |

Invalid 5: line 3139 (field count), `FRN`, `Yes`, empty ballot, NZL. Contradiction 151: FRA in vote 012 (2) and all of vote 025 (149). Vote 027's slash dates are now normalised, so all 149 rows are new.

For [Lock the database schema](16-lock-database-schema.md): the conflict's compared-against revision link, two conflict subjects (ballot, vote details), several conflict links per row record, the decision as origin for membership-recalculated ballots, per-row normalisation records, reason codes, and file-level warnings on the import.
