# Research: audit of the historic file

Researched: 2026-10-28. The primary sources are the data files in the gitignored `candidate_data/`. Every number below comes from a script in `17-historic-audit-scripts/`, which reuses the loaders of [07-data-audit-scripts](07-data-audit-scripts/) plus a reference classifier for the rules in [Define import classification rules](../issues/08-import-classification.md). Re-run everything (about 3 minutes) with:

```
bash .scratch/meridian-vote-tracker/research/17-historic-audit-scripts/run_all.sh
```

| Script | Covers |
|---|---|
| `common.py` | loaders, checksums, member_states periods, the 08 normalisations, and `Store`, a reference classifier for 08 rules 2–7 and 12 |
| `01_structure.py` | SHA-256 of all four files, bytes, encoding, BOM, line endings, header, field counts, quoting, shifted rows |
| `02_fields.py` | per-field profiles: codes, names, ballots, date shapes, impossible dates, session vs year, vote ids, published_at |
| `03_votes.py` | duplicates, contradictions, vote-detail variants, rows per vote, file order, reused titles |
| `04_membership.py` | membership on the vote date, ISL/NZL/MNE/SSD/TLS boundaries, completeness per vote |
| `05_overlap.py` | overlap with both Q1 exports |
| `06_simulate.py` | predicted outcomes: historic into an empty system, Q1 alone, historic then Q1, and Q1 then historic |
| `07_shape.py` | file shape for the 5-minute budget and the synthetic benchmark |

If `member_states.csv` or either Q1 export is missing, each check that needs it prints `pending: file missing: <path>` and is skipped, and the remaining checks still run. Line numbers are 1-based physical lines, with line 1 as the header. No record spans two lines.

**Inputs verified.** All four files match the recorded SHA-256 (historic: [Bring in the historic file](../issues/13-historic-file.md); member_states and the Q1 files: the full hashes behind the prefixes in [Place the data files locally](../issues/01-place-data-files.md)). The member_states and Q1 files re-uploaded on 2026-10-28 are therefore byte-identical to the ones 07 audited.

## 0. Headline

- 962,172 lines: 962,171 records plus the header. 6,480 votes (`MA/2015/001` to `MA/2023/720`), 150 member codes plus 3 junk code values.
- The file is very clean. **62 problem rows (0.006 %)**: 30 exact duplicates, 11 truncated rows, 4 column-shifted rows, 5 bad ballots, 10 placeholder `XXX` rows and 2 non-member rows.
- There are no ballot contradictions, no vote-detail contradictions (once the shifted rows are set aside), no date problems and no name variants.
- It shares nothing with the Q1 exports: no vote ids, no (vote, state) keys and no dates. Importing it before or after Q1 gives the same counts.
- Predicted import into an empty system under the 08 rules: **962,109 new / 30 duplicate / 32 invalid / 0 contradiction**. Fifteen votes are left incomplete by one state each.

## 1. File-level structure (`01_structure.py`)

| Property | historic | (Q1 original / rev2, from 07) |
|---|---|---|
| Size | 103,625,965 bytes (98.8 MiB), 107.7 bytes/line | 0.6 MB each |
| Encoding | UTF-8, valid; non-ASCII only `ã é í ô ü` | same |
| BOM | **no** | yes / no |
| Line endings | LF only, final newline, no bare CR | LF / CRLF |
| NUL, TAB, NBSP, zero-width, control | none | none |
| Blank lines; whitespace at line ends | none; none | none |
| Header | exact Appendix A header, `resolution_title` included | same |
| Field counts | 962,160 × 8, **11 × 7** | |
| Quoting | 233,991 quoted titles, all containing a comma; no `""` escapes; no odd quote counts | |
| Row order | **fully interleaved**: 962,022 runs of the same vote id in 962,171 rows | grouped / shuffled |

## 2. The 25 known defect classes on the historic file

| Class | Occurs? | Count in historic | Note |
|---|---|---|---|
| D-01 BOM | no | 0 | |
| D-02 line endings | LF | n/a | parser already accepts LF and CRLF |
| D-03 wrong field count | **yes, new kind** | 11 | 7 fields, not a `;` delimiter: see N-1 |
| D-04 unknown code | **yes, new kind** | 10 `XXX` (+4 shifted) | a placeholder, not a typo: see N-3 and N-2 |
| D-05 lower-case code | no | 0 | |
| D-06 ballot word | yes | 1 (`YES`) | plus 4 non-word bad ballots: see N-4 |
| D-07 empty ballot | no real case | 0 (4 only as a shift artefact) | |
| D-08 non-member on the vote date | **yes, new direction** | 2 | before joining, not after leaving: see N-5 |
| D-09 non-ISO date | no | 0 | every `vote_date` is `yyyy-mm-dd`, all 540 values are real dates, and there are no Feb 29 values |
| D-10 exact duplicates | yes | 30 extra copies (30 pairs) | never adjacent to the original: see N-7 |
| D-11 ballot contradiction | no | 0 | 30 (vote, state) pairs repeat, all with the same ballot |
| D-12 vote-detail inconsistency (title) | only as a shift artefact | 4 rows in 4 votes | gone once those rows are invalid (08 rule 3 order) |
| D-13 vote-detail inconsistency (date) | no | 0 | |
| D-14 partial vote | yes, mild | 15 votes, 1 state missing each | 10 from `XXX`, 5 from bad ballots. No truncated votes. |
| D-15 name differs from member_states | no | 0 | every code has one name, which equals member_states. `TUR` is `Türkiye`. |
| D-16 / D-17 change vs approved data | n/a | 0 | no shared votes with Q1 (§5) |
| D-18 new vote | all | 6,480 | |
| D-19 / D-20 absence | n/a | 0 | |
| D-21 shuffled order | **yes, extreme** | all 6,480 votes | a vote's rows span 92–100 % of the file (median 98.9 %) |
| D-22 `published_at` | **changed meaning** | | constant **per vote** (vote_date + 2 days, 09:00:00Z), not per file: see N-6 |
| D-23 titles shared across votes | **yes, heavy** | 224 titles for 6,480 votes | up to 45 votes per title. 151 (title, date) pairs belong to more than one vote. 34 of the 36 Q1 titles also appear in historic. |
| D-24 session | consistent | 0 | session = year − 1945 on every row (70 to 78, and Q1 is 79). Only the 2007 scan breaks the rule. Keep "never checked against the year". |
| D-25 weekend dates | yes, not a defect | 288,587 rows | |

Other checks with nothing found: ambiguous day/month (no non-ISO dates exist, so day-first reading is never exercised), impossible dates, case variants, padding in real values, double spaces, vote ids off the `MA/yyyy/nnn` pattern (0), id year ≠ date year (0), and gaps in id numbering (each year runs 001–720 with no gaps, and id order follows date order).

### New defect classes

- **N-1 Truncated rows (11).**
  - Each has 7 fields: `country_name` is empty and `published_at` is missing, e.g. line 134746 ends `...,LVA,,Y`.
  - **Every one is an extra copy of a well-formed row of the same vote and state, with the same ballot** (e.g. LVA's full row is line 309668).
  - 08 rule 1 makes them invalid (field count), even though all required fields are present.
- **N-2 Column-shifted rows (4).**
  - These are lines 521760, 687843, 736765 and 922345.
  - A title with a comma was left unquoted, e.g. `Ocean Plastic Pollution, Including Capacity Building`, so every later column moves one place right.
  - The trailing `published_at` is also missing, so the row still has **8 fields** and passes the field-count check.
  - The result: `country_code` = ` Including Capacity Building`, the name holds the real code, `ballot` is empty and `published_at` holds the ballot.
  - All 4 are extra copies of well-formed rows with the same ballot.
  - Under 08 they come out invalid, but only because the shifted ballot is empty (reason "required field empty") or the code is unknown. A shifted row whose values happened to look valid would pass.
- **N-3 Placeholder code (10).** `XXX,Unknown` with a valid ballot (e.g. line 555441). In each case it replaces one real member, so that vote is missing exactly one state.
- **N-4 Non-word bad ballots (5).** `0` (lines 235011, 288625, 626224), `?` (340903) and upper-case `YES` (319853). Each is the only row for its state in that vote.
- **N-5 Vote before joining (2).** SSD on 2015-01-08 (line 905768; SSD joined 2016-03-01) and TLS on 2022-03-11 (line 328072; TLS joined 2023-01-01). Each is an extra row on top of a complete vote. 07's D-08 was the other direction (NZL after leaving).
- **N-6 `published_at` varies within one file.** There are 540 distinct values, one per vote day, always vote_date + 2 days at 09:00:00Z. The Q1 files have one value per file.
- **N-7 Duplicates scattered.** The 30 copies sit far from their originals (e.g. lines 4251 and 270513), so only whole-file grouping finds them.

### Membership boundaries (`04_membership.py`)

- ISL has no rows from 2020-01-01 to 2021-12-31. Its rows run 2015–2019 and 2022–2023.
- NZL's last row is 2021-06-26. Its exit is 2021-06-30.
- MNE appears in every vote. SSD (apart from N-5) starts 2016-03-01. TLS (apart from N-5) starts 2023-01-01.
- **No row falls exactly on a `member_from` or `member_to` day**, so 08 rule 12's "inclusive" is not exercised by any real data.
- Members on a vote date are 147 (420 votes), 148 (2,556) or 149 (3,504).

## 3. Predicted import outcomes (`06_simulate.py`)

`common.Store` implements 08 rules 1–7 and 12: header-driven field count, the rule 2 normalisations and comparison key, first-match order invalid → contradiction → duplicate → new/unchanged/conflict, whole-vote hold-back for detail contradictions, per-vote detail conflicts, and pending-conflict re-linking.

**Cross-check:** on the Q1 files it reproduces 08's hand-derived table exactly:
- original: 5,737 new / 3 / 5 / 151
- rev2: 372 / 5,284 unchanged / 304 conflict rows, with 6 ballot and 2 details conflicts opened

| Import | new | unchanged | duplicate | invalid | contradiction | conflict |
|---|---|---|---|---|---|---|
| historic into an empty system | **962,109** | 0 | **30** | **32** | 0 | 0 |
| historic again | 0 | 962,109 | 30 | 32 | 0 | 0 |

How the 32 invalid rows break down:

| Reason code | Rows | Which rows |
|---|---|---|
| field count | 11 | N-1 |
| required field empty | 4 | N-2 |
| unknown country code | 10 | `XXX` |
| ballot | 5 | N-4 |
| not a member on vote date | 2 | N-5 |

After the import, 962,109 ballots in 6,480 votes are approved. **15 incomplete-vote warnings** (rule 11) are raised, each missing one state.

**Q1 and historic in either order** (scenarios C and D): each file gets exactly the counts it gets alone. That gives 968,218 approved ballots in 6,521 votes, with 6 ballot and 2 details conflicts pending. Nothing interacts because no vote id, (vote, state) key or date is shared (§5). The classifier does not model warnings. Two warnings follow from the data:
- Q1 then historic: rule 10's "published_at earlier than the newest imported" fires, because every historic value is earlier than 2024-04-02.
- In either order, historic adds the 15 incomplete-vote warnings.

**Sensitivity:** if a row missing only trailing *optional* columns were padded rather than invalid, the 11 N-1 rows would become **duplicate**. That gives 962,109 new / 41 duplicate / 21 invalid.

## 4. Shape for the 5-minute budget (`07_shape.py`)

For a synthetic benchmark file that mirrors this one:

| Measure | Value |
|---|---|
| Size | 98.8 MiB, 962,171 records, 107.7 bytes/line, LF, no BOM |
| Votes | 6,480 = 9 years × 720. 60 vote days a year, **exactly 12 votes per vote day** (540 days). |
| Rows per vote | min 147, p10 148, **median 149**, p90 149, max 151, mean 148.48 |
| Expected rows per vote | 147 / 148 / 149 members on the date (ISL gap, NZL exit, SSD and TLS joins) |
| Distinct states | 150 member codes (153 `country_code` values with the junk) |
| Order | **fully interleaved**: consecutive rows almost never share a vote id, so a vote is only complete at end of file |
| Titles | 224 distinct, 13–74 chars. 24.3 % of rows have a quoted title with a comma. |
| Ballot mix | Y 48.4 %, N 28.8 %, A 15.0 %, X 7.8 % |
| Reference timing | the Python reference classifier (parse plus classify, no database, one core) takes roughly 25 s per historic pass here. That is only a floor for the parse and grouping work. |

## 5. Overlap with the Q1 exports (`05_overlap.py`)

Against both the original and rev2:

| Shared item | Count |
|---|---|
| Vote ids | 0 |
| (vote, state) keys | 0 |
| Vote dates | 0 (historic 2015-01-07..2023-12-26; Q1 2024-01-12..2024-03-16) |
| Sessions | 0 (70–78 vs 79) |
| Titles | 34 of 36 Q1 titles appear in historic (case- and space-folded) |

So: no same-or-different vote details, no ballot comparisons, and no conflicts in either import order.

## 6. Flags for [Lock the database schema](../issues/16-lock-database-schema.md)

1. **Column name.** 08 rule 1 lists the required column as `title`, but every real file (and Appendix A) calls it `resolution_title`. Header matching must accept `resolution_title`, either as the canonical name or a declared alias. Otherwise every import **fails**.
2. **Rows missing only optional trailing columns** (N-1, 11 rows): 08 makes them invalid by field count. Decide whether that stays, or whether optional columns may be absent (then they become duplicates). Either way, give them a distinct reason code (e.g. `missing_trailing_columns`) rather than a generic field-count reason.
3. **Shifted columns** (N-2): a row with an unquoted comma can keep the right field count and still be misread. Consider a reason code `columns_shifted`, detected when `published_at` doesn't look like a timestamp or the code slot holds text. The raw line is already kept on the row record (rule 13).
4. **Reason-code list** needs at least: field count / missing trailing columns, columns shifted, required field empty, unknown code (placeholder `XXX` included), ballot, date, session, not a member (before joining / after leaving). Store these as a lookup or check-constrained code.
5. **`published_at` is per row, not per file** (N-6). Store it on the row record (or per vote), not on the import. Rule 10's warning must say which value it compares (the file's max?). A historic back-fill after Q1 will always trigger it, so the warning text should say so, not alarm.
6. **Volume.** One historic import writes about 962k insert-only row records (raw text about 100 MB) and about 962k ballot revisions. Rule 13 says counts are queried from row records and never stored. That needs an index such as `(import_id, outcome)` and `(import_id, vote_id)`, or the job's progress counters must carry the totals for live display.
7. **Whole-file grouping is mandatory** (D-21, N-7). No vote is complete before the last line, so the parser must hold the file (about 1M rows) in memory, or stage it, before classifying 6,480 per-vote transactions. Hand this to [Design background jobs and live progress](../issues/10-background-jobs.md) and the benchmark.
8. **Incomplete-vote warnings** are structured: vote plus the missing codes, 15 here. Store them as rows (e.g. `import_warning(import_id, kind, vote_id, detail)`) rather than free text.
9. **Vote identity is the id only.** There is no unique index on (title, date), because 151 (title, date) pairs legitimately belong to several votes.
10. **Vote id format** is not validated by 08. Every real id fits `MA/yyyy/nnn`, with id year = date year. If a check is added, nothing in the data fails it.
11. Membership boundary days are never hit by real data. Keep a unit test for inclusive `member_from`/`member_to`, since the data won't catch a regression.
12. **Sizing after all three files:** about 968k approved ballots, 6,521 votes and 150 states.

## 7. Pending

Nothing is pending. `member_states.csv` and both Q1 exports were uploaded on 2026-10-28, are byte-identical to what 07 audited, and every membership and overlap check above ran against them. The scripts still skip cleanly (`pending: file missing: ...`) if a later container lacks those files.
