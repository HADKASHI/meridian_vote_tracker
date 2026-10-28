# Research: data audit of the candidate files

Researched: 2026-10-26. The primary sources are the data files themselves in `data_candidate/` (gitignored). Every claim below is backed by a script in `07-data-audit-scripts/` or a shell one-liner shown inline. Re-run everything with:

```
bash .scratch/meridian-vote-tracker/research/07-data-audit-scripts/run_all.sh
```

| Script | Covers |
|---|---|
| `common.py` | loaders (csv module + physical line numbers), membership check |
| `01_structure.py` | bytes, encoding, BOM, line endings, header, field counts, quoting |
| `02_fields.py` | per-field profiles: empty, padding, case, codes, ballots, date shapes, session |
| `03_votes.py` | member_states, membership on vote date, per-vote completeness/dups/contradictions/details |
| `04_diff.py` | rev2 vs original, keyed on (vote id, country code) |
| `05_simulate.py` | reference classifier: predicted outcomes for orig -> rev2 -> rev2, with assumption switches |

Line numbers are 1-based physical lines, so line 1 is the header. No record in any file spans more than one line, which `01_structure.py` checks ("records with embedded newline: []"), so physical line equals record line + 1.

**Not available:** `assembly_historic.csv` (2015–2023, about 1M rows) is not in `data_candidate/`. Nothing here was checked against it. Defect classes that only show up across years can't be audited yet: sessions across years, Iceland's 2020–2021 gap, the New Zealand exit in 2021-06-30, the South Sudan and Timor-Leste joins, and volume or performance.

## 0. Headline numbers

| | original (`_q1.csv`) | rev2 (`_q1_rev2.csv`) | member_states |
|---|---|---|---|
| physical lines (incl. header) | 5,897 | 5,961 | 152 |
| data records | 5,896 (5,895 with 8 fields + 1 malformed) | 5,960 | 151 rows, 150 codes |
| votes | 40 (`MA/2024/001`–`040`) | 40 (`001`–`039`, `041`) | |
| expected rows (40 or 41 votes x 149 members in 2024) | 5,960 | 5,960 (40 x 149) | |

In 2024 there are 149 members: 150 codes minus NZL, which left on 2021-06-30. Iceland is a member again from 2022-01-01. MNE, SSD and TLS are members.

The brief says rev2 has "same votes ... and one new vote", but rev2 has exactly 40 votes. It adds `041` and **drops `040`** entirely (see D-20).

## 1. File-level structure (`01_structure.py`)

| Property | original | rev2 | member_states |
|---|---|---|---|
| Encoding | UTF-8 (valid) | UTF-8 (valid) | UTF-8 (valid) |
| BOM | **yes (EF BB BF)** | no | no |
| Line endings | **LF** (5,897 LF, 0 CR) | **CRLF** (5,961) | CRLF (152) |
| Final newline | yes | yes | yes |
| Blank lines / NUL / TAB | none | none | none |
| Leading/trailing whitespace on a line | none | none | none |
| Header | exact match to Appendix A (after the BOM) | exact match | `country_code,country_name,member_from,member_to` |
| Field counts | 5,896 x 8 + **1 x 5** (line 3139) | 5,960 x 8 | 151 x 4 |
| Odd quote count on a line | 0 | 0 | 0 |
| Non-ASCII characters | `ã é í ô ü` (Côte d'Ivoire, São Tomé and Príncipe, Türkiye) | `ã é í ô` (no `ü`: Turkey) | same as original |
| Row order | grouped by vote (40 contiguous runs), states in member_states order | **shuffled** (5,790 runs for 40 votes) | alphabetical by name |

Other parsing traps that are valid CSV:
- Titles that contain commas are quoted. `01_structure.py` counts 1,641 quoted segments with a comma in the original and 1,639 in rev2. The regex is approximate because of the `""` escapes below. Example: `"Desertification, Including Capacity Building"`.
- Vote `036` (149 rows in each file) has escaped quotes inside a quoted field: `"Follow-up to the ""Blue Ocean"" Declaration, Including Capacity Building"`. You can see this with `grep -c '""'` on each file, which gives 149 and 149. A naive `split(',')` or a regex parser breaks on it.

## 2. Defect catalogue

Each defect class lists its definition, counts, examples, requirement, proposed ING-06 outcome and any open question. Counts are original / rev2.

### D-01 Byte-order mark
- **Definition:** the file starts with a UTF-8 BOM.
- **Counts:** original 1 (file-level), rev2 0.
- **Example:** line 1 of the original starts with bytes `EF BB BF` before `assembly_vote_id`.
- **Requirement:** ING-04 structure.
- **Outcome:** not a row outcome. Strip the BOM, otherwise header matching fails (the first column becomes `﻿assembly_vote_id`). Document it as a normalisation (ING-13).

### D-02 Mixed line endings across files
- **Definition:** the files use different line endings.
- **Counts:** original LF, rev2 CRLF.
- **Requirement:** ING-04 structure.
- **Outcome:** not a row outcome. The parser must accept both. Unchecked risk: with CRLF, a naive line split leaves `\r` in `published_at`.

### D-03 Wrong field count / wrong delimiter
- **Definition:** a record with a field count other than 8.
- **Counts:** original 1, rev2 0.
- **Example (orig line 3139):** row elided (the data stays out of git; open the line in `data_candidate/`). This has 5 fields. A `;` replaces the comma, and `country_name` and `published_at` are missing.
- **Requirement:** ING-04 structure.
- **Outcome:** invalid.
- **Open question:** we should not try to repair it by splitting on `;` (ING-13).
- **Effect:** vote `022` in the original lacks AGO. rev2 supplies AGO (rev2 line 4798), so that row is new on the rev2 import.

### D-04 Unknown country code (typo)
- **Definition:** `country_code` is not in member_states and no case or whitespace change fixes it.
- **Counts:** original 1, rev2 0.
- **Example (orig line 2143):** row elided (the data stays out of git; open the line in `data_candidate/`).
- **Diagnosis:** `FRN` is not a real ISO 3166-1 alpha-3 code (France is `FRA`). The name "France" shows it is a typo. Vote `015` then has no FRA row.
- **Requirement:** ING-04 country code.
- **Outcome:** invalid.
- **Rev2 effect:** rev2 has FRA at line 3877, ballot X, which is new.
- **Open question:** should we infer FRA from the name? Recommendation: no (ING-13 says report problems, don't fix them).

### D-05 Lower-case country code
- **Definition:** a code whose upper-case form is a member.
- **Counts:** original 1, rev2 0.
- **Example (orig line 2594):** `MA/2024/018,...,deu,Germany,X,...`.
- **Requirement:** ING-04 and ING-13. The brief names lower-case codes as a normalisation that must be documented.
- **Outcome:** normalise to `DEU`, record the normalisation, and mark the row new. Under that rule, rev2's `DEU` row (line 1849) is unchanged. If we don't normalise, the original row is invalid and the rev2 row is new.

### D-06 Ballot spelled out
- **Definition:** a ballot value outside {Y, N, A, X} that is a word.
- **Counts:** original 1 (`Yes`), rev2 0.
- **Example (orig line 2842):** `MA/2024/020,...,ARG,Argentina,Yes,...`.
- **Requirement:** ING-04 ballot.
- **Outcome:** invalid.
- **Trap:** rev2 line 952 gives ARG **`A`**, not Y. If the importer normalised `Yes` to `Y`, rev2 would raise a spurious conflict (Y->A). Treating `Yes` as invalid makes the rev2 row new. This is a strong argument against "helpful" normalisation of ballot words.

### D-07 Empty ballot
- **Definition:** the ballot field is empty.
- **Counts:** original 1, rev2 0.
- **Example (orig line 2846):** `MA/2024/020,...,AZE,Azerbaijan,,2024-04-02T09:00:00Z`.
- **Requirement:** ING-04 required field.
- **Outcome:** invalid. Do not default it to `X`.
- **Rev2:** gives `A` (line 3175), which is new.

### D-08 Non-member on the vote date
- **Definition:** a known code that has no membership period covering the vote date.
- **Counts:** original 1, rev2 0.
- **Example (orig line 4854):** row elided (the data stays out of git; open the line in `data_candidate/`). NZL's membership ended on 2021-06-30. NZL is a real ISO code and a real former member, so it is not a typo.
- **Requirement:** ING-04 membership.
- **Outcome:** invalid. rev2 omits NZL from `033`, so nothing follows from it.
- **Not hit in Q1 2024:** Iceland is present in every vote and is a valid member since 2022-01-01. MNE, SSD and TLS are present and valid. These classes need the historic file to be exercised.

### D-09 Date in a non-ISO format
- **Definition:** `vote_date` not in `yyyy-mm-dd`.
- **Counts:** original 6, rev2 0.
- **Example (orig lines 3880–3885, vote `027`):** row elided (the data stays out of git; open the line in `data_candidate/`). The other 143 rows of `027` use `2024-02-21`.
- **What the files use:** every other date in both files (5,889 + 5,960 rows) is `yyyy-mm-dd`. `published_at` is always `yyyy-mm-ddThh:mm:ssZ`. The odd format is `dd/mm/yyyy` with **slashes**, which matches neither Appendix A (`yyyy-mm-dd`) nor ING-04 (`dd-mm-yyyy`). No date in any file uses `dd-mm-yyyy`. No impossible dates: all parse, and 2024-02-29 (vote `034`) is a valid leap day.
- **Requirement:** ING-04 date format. It is also an ING-05 detail inconsistency if accepted as text.
- **Outcome:** invalid (recommended). If accepted and normalised, the 6 rows are new and consistent with the vote.
- **Open question:** strict `yyyy-mm-dd`, or also accept the brief's `dd-mm-yyyy` and/or `dd/mm/yyyy`? See the open questions.

### D-10 Exact duplicate rows
- **Definition:** a row byte-identical to another row in the same file.
- **Counts:** original 3 extra copies, rev2 0.
- **Example (orig lines 1045–1047)** repeat lines 896–898, which are AFG Y, ALB A and DZA N of vote `007`. The copies are appended after the vote's last row (ZMB, line 1044). Check with `sort assembly_export_2024_q1.csv | uniq -cd`, which gives 3 lines with count 2.
- **Requirement:** ING-06 duplicate.
- **Outcome:** the copies are duplicate, and the first occurrence is new.
- **Open question:** is the first copy also "duplicate"? With `DUP_FIRST_IS_NEW=0`, all 6 rows are duplicate and rev2 then adds 3 more new rows.

### D-11 Same state, different ballots (contradiction)
- **Definition:** two rows for the same (vote, state) with different ballots.
- **Counts:** original 1 pair (2 rows), rev2 0.
- **Example (orig lines 1695 and 1793, vote `012`):** `...,FRA,France,Y,...` and `...,FRA,France,N,...`. The second row is appended after ZMB.
- **Requirement:** ING-05 and ING-06 contradiction.
- **Outcome:** both rows are contradiction, and neither is stored.
- **Rev2:** gives FRA **`A`** (line 2717), which matches neither of the contradicting values. It becomes new (nothing was approved).

### D-12 Inconsistent vote details within a vote (title)
- **Definition:** rows of one vote that disagree on session, date or title.
- **Counts:** original 1 vote (`025`), 2 deviating rows, rev2 0.
- **Examples:**
  - Line 3592: `Nuclear  Non-Proliferation and the Role of Regional Organizations` (double space, BHS).
  - Line 3593: `Nuclear Non-Proliferation and the Role of Regional Organizations (draft)` (BHR).
  - The other 147 rows use the plain title.
- **Requirement:** ING-05 and ING-06 contradiction.
- **Outcome:** contradiction.
- **Open question:** only the 2 deviating rows, or the whole vote (149 rows)? See the open questions.
- **Open question:** should whitespace in titles be collapsed? If yes, line 3592 becomes consistent. That would be a normalisation to document.
- **Rev2:** has all 149 rows with the plain title, including BHS (line 5546) and BHR (line 5174).

### D-13 Inconsistent vote details within a vote (date format)
- **Definition:** the same as D-09 but seen per vote. Vote `027` has 2 distinct `vote_date` strings that denote the same day.
- **Outcome:** if D-09 makes these rows invalid, D-13 never fires.

### D-14 Partial vote (truncated)
- **Definition:** a vote with fewer rows than members on its date.
- **Counts:** original 1 vote (`031`, 80 of 149 rows, missing 69 states), rev2 0.
- **Details:** the 80 rows are exactly the first 80 states in member_states order (AFG…LBR, lines 4476–4555). The export was cut off after Liberia. Liberia is also the last state on page 1 of the scanned sheets.
- **Other incomplete votes in the original:** `015` (FRA missing because of FRN), `022` (AGO missing because of D-03) and `033` (complete, plus an extra NZL).
- **Requirement:** ING-10.
- **Outcome:** the 80 rows are new. In rev2, the 69 missing states (e.g. CHE, line 4373) are **new, not conflicts**.
- **Open question:** should the importer warn that a vote is incomplete relative to membership on that date? The brief doesn't require it, but it is cheap and useful.

### D-15 Country name differs from member_states
- **Definition:** a known code with a `country_name` other than the member_states name.
- **Counts:** original 0, rev2 **40 rows** (TUR in all 40 votes): `Turkey` vs member_states `Türkiye`.
- **Example (rev2 line 236):** row elided (the data stays out of git; open the line in `data_candidate/`).
- **Requirement:** ING-11.
- **Outcome:** not a conflict, and not invalid. Compare on code. 38 of these rows are otherwise identical to the original and should be **unchanged**.
- **Other name issues:** no file uses two names for one code. In the original, two names map to two codes ("France": FRA and FRN; "Germany": DEU and deu), which are D-04 and D-05.
- **Open question:** store the published name per row (as published), or only the canonical name?

### D-16 Ballot changed vs approved data (correction)
- **Definition:** in rev2, a valid approved ballot changed to a different valid ballot.
- **Counts (rev2): 6.**

  | Vote | State | Old -> new | Orig line | Rev2 line |
  |---|---|---|---|---|
  | `003` | BRA | Y -> A | 321 | 5353 |
  | `003` | IND | Y -> N | 366 | 5378 |
  | `010` | JPN | Y -> X | 1417 | 3745 |
  | `019` | ZAF | Y -> N | 2816 | 5426 |
  | `028` | CAN | Y -> N | 4058 | 2983 |
  | `028` | MEX | Y -> A | 4120 | 4378 |

- **Not counted as conflicts:** three more rows differ from the original, but nothing was approved for them. These are FRA in `012` (contradiction Y/N -> A), ARG in `020` (`Yes` -> A) and AZE in `020` (empty -> A). They are **new**.
- **Requirement:** ING-06.
- **Outcome:** conflict.

### D-17 Vote details changed vs approved data
- **Definition:** in rev2, a vote's session, date or title differs from what was stored.
- **Counts (rev2): 2 votes.**
  - `009`: title `Biological Diversity in Coastal Zones` -> `Biological Diversity in Coastal Zones: Revised`. Orig line 1197 vs rev2 line 45. All 149 ballots are unchanged.
  - `014`: `vote_date` `2024-02-05` -> `2024-02-06`. Orig line 1943 vs rev2 line 32. All 149 ballots are unchanged.
- **Requirement:** ING-06 conflict ("differs from approved data already stored").
- **Outcome:** conflict.
- **Open question:** is it one vote-level conflict, or 149 row conflicts? The simulation counts rows (298).

### D-18 New vote in rev2
- **Definition:** a vote id not present in approved data.
- **Details:** `MA/2024/041`, session 79, 2024-03-16, "Protection of Cultural Heritage and Regional Cooperation", 149 rows, complete. Example: rev2 line 1899.
- **Outcome:** new.

### D-19 Rows of approved data absent from rev2
- **Definition:** a state that appears in the original but not in rev2.
- **Counts:** 3 (`015` FRN, `018` deu, `033` NZL). All three were invalid in the original, or normalised to a code rev2 does provide (deu -> DEU).
- **Outcome:** no row outcome. Absence is not deletion.

### D-20 Whole vote absent from rev2
- **Definition:** a vote in the original but not in rev2.
- **Details:** `MA/2024/040` (2024-03-14, "Desertification: Implementation Review", 149 rows). The brief says rev2 contains the "same votes", but it doesn't.
- **Outcome:** none. Keep the approved data. ING-10 (votes arrive in parts) implies absence means nothing.
- **Open question:** should the import report list "approved votes not in this file" for information?

### D-21 Shuffled row order
- **Definition:** a vote's rows are not contiguous.
- **Counts:** original 0 votes, rev2 all 40.
- **Outcome:** none. The importer must not assume grouping or streaming per vote.

### D-22 `published_at` differs between files
- **Details:** constant within each file: `2024-04-02T09:00:00Z` (orig) and `2024-04-20T09:00:00Z` (rev2). **No rev2 row is identical to an original row in all 8 fields** (`04_diff.py`: 0). Ignoring `published_at`, 5,388 rows match. Ignoring `published_at` and `country_name`, 5,424 match.
- **Outcome:** "unchanged" must compare ballot and vote details, not whole rows.
- **Open question:** should `published_at` order the files ("uploaded after the original") or only the upload time?

### D-23 Shared titles across distinct votes
- **Details:** not a defect, but a trap. Five title pairs are reused across votes in the original, and four in rev2:
  - `003`/`004` "Food Security in Coastal Zones", **both on 2024-01-16, session 79**
  - `022`/`029`
  - `028`/`035`
  - `011`/`039`
  - `009`/`023` in the original only
- **Outcome:** none. Vote identity must be `assembly_vote_id`, never title or date.

### D-24 Session
- **Details:** session is `79` on every row of both files (integer, no variants), so there is no in-file defect.
- **Cross-year note (scans):**
  - MA/2007/015 is session 61 (14 March 2007).
  - MA/2009/117 is session 64 (12 Nov 2009).
  - 2024 is session 79.
  - No single "session = year - k" rule fits all three: 2024-79 = 1945, 2009-64 = 1945, 2007-61 = 1946.
  - An autumn-start rule fits 2007 and 2009 but would make Jan–Mar 2024 session 78.
- **Outcome:** don't validate session against year. Only check it's an integer and consistent within a vote.

### D-25 Weekend vote dates
- **Details:** 12 of 41 votes fall on a Saturday or Sunday (e.g. `006` 2024-01-21 is a Sunday).
- **Outcome:** not a defect in fictional data. Don't validate it.

### member_states.csv
- 151 rows and 150 codes. No duplicate names, no padding, and all dates are `yyyy-mm-dd` with from <= to.
- **ISL** has 2 periods (1995-01-01–2019-12-31 and 2022-01-01–open).
- Non-default periods: **NZL** 1995-01-01–2021-06-30, **MNE** from 2007-01-01, **SSD** from 2016-03-01, **TLS** from 2023-01-01.
- Not ordered by code. No defects found.
- Membership must be a period lookup, not a code -> single row map, because ISL would collide.
- **Open question:** is `member_to` inclusive? The audit assumes yes, since 2019-12-31 is a plausible last day.

## 3. rev2 vs original: summary (`04_diff.py`)

| Category | Count | Detail |
|---|---|---|
| Ballot changes on approved values | 6 | D-16 table |
| Ballot "changes" on non-approved values | 3 | FRA@012 (contradiction), ARG@020 (`Yes`), AZE@020 (empty), all now `A` |
| Vote-detail changes | 2 votes | `009` title, `014` date |
| Original-only detail problems fixed | 2 votes | `025` titles unified, `027` dates unified |
| States added (completions) | 72 | 69 in `031`, FRA in `015`, DEU in `018` (as code change from `deu`), AGO in `022` |
| States absent in rev2 | 3 | FRN@015, deu@018, NZL@033 (all bad in the original) |
| New vote | 1 | `041` |
| Vote absent | 1 | `040` |
| Duplicates removed | 3 | `007` |
| Name change | 40 rows | TUR Türkiye -> Turkey (38 in common votes plus `031`/`041`) |

rev2 resolves every original-file problem: D-03, D-04, D-05, D-06, D-07, D-08, D-09, D-10, D-11, D-12 and D-14. rev2 itself has no row-level validation defects. Its only "defects" are the conflicts (D-16, D-17), the name variant (D-15), the shuffle (D-21) and the dropped vote (D-20).

## 4. Voting-sheet PDFs (bonus; visual inspection at 100 dpi only)

`pdfinfo`/`pdfimages` show both files are 2-page A4 PDF 1.4, each page a single 1654x2339 JPEG at 200 dpi with **no text layer** (`pdftotext` returns nothing). OCR is needed, and `tesseract` is not installed here. Each sheet has a header (Vote No., Session, Date of vote in long form like "14 March 2007", Resolution), a code/name/YES/NO/ABST. grid in member_states order, "Recorded totals" and a "Certified correct" line.

- **MA_2007_015.pdf** (session 61, 14 March 2007, "Landmine Clearance: Implementation Review"). Clean, straight scan. 149 rows on the sheet.
  - Includes **ANT Netherlands Antilles**, which is not in member_states (ANT is a withdrawn ISO code), giving 148 members + 1.
  - Includes MNE (joined 2007-01-01, so valid) and NZL. No SSD or TLS.
  - **BEL**: circled YES and a struck-out NO with initials "R.K." (a correction).
  - **GUY**: ABST struck out with "R.K." and no other mark.
  - **NIC**: both YES and NO marked (a contradiction).
  - Several blank rows (not voting = `X`).
  - Recorded totals: Yes 16, No 87, Abst 36, Not voting 9 (sum 148). The totals were not tallied against the marks.
- **MA_2009_117.pdf** (session 64, 12 Nov 2009, "Freedom of Navigation, Navigation and Trade").
  - **Skewed and speckle-noisy** scan, so it needs deskewing and denoising before OCR or mark detection.
  - The column split differs from the 2007 sheet: page 2 ends the left column with SMR.
  - Recorded totals: Yes 83, No 43, Abst 9, Not voting 13 (sum 148).
  - No anomalies seen at this resolution. A higher-resolution pass and a tally against the totals are needed.
- Sheet date format (`14 March 2007`) differs from both CSV formats.

Defect classes these sheets introduce: hand corrections with initials, double marks, a non-member code, blank rows, recorded totals to reconcile, and skewed scans.

## 5. Expected import results (`05_simulate.py`)

**Baseline assumptions**, each a switch in the script:
1. Lower-case codes are normalised and documented: `NORMALISE_CODE_CASE=1`.
2. `Yes` is invalid: `YES_AS_Y=0`.
3. Only `yyyy-mm-dd` is accepted: `ACCEPT_SLASH_DATE=0`.
4. A detail inconsistency marks only the minority rows as contradiction (majority details win): `VOTE_LEVEL_DETAIL_CONTRA=0`.
5. The first copy of a duplicate is processed normally and later copies are duplicate: `DUP_FIRST_IS_NEW=1`.
6. A row is classified once, in this order: invalid, duplicate, contradiction, then comparison with stored data.
7. Rows compare on (vote id, code). "Unchanged" ignores `country_name` and `published_at`.
8. A vote-detail change makes every row of that vote a conflict.
9. New rows are approved immediately, and conflicts are not applied.
10. Absence implies nothing.
11. On the third import, the conflicts are still open.

| Import | new | unchanged | duplicate | invalid | contradiction | conflict | total |
|---|---|---|---|---|---|---|---|
| 1. original | **5,878** | 0 | **3** | **11** | **4** | 0 | 5,896 |
| 2. rev2 | **231** | **5,425** | 0 | 0 | 0 | **304** | 5,960 |
| 3. rev2 again (conflicts unresolved) | 0 | **5,656** | 0 | 0 | 0 | **304** | 5,960 |
| 3'. rev2 again (all conflicts accepted first) | 0 | 5,960 | 0 | 0 | 0 | 0 | 5,960 |

How the totals break down:
- **Invalid 11:** 3139 field count, 2143 FRN, 2842 `Yes`, 2846 empty, 4854 NZL, and 3880–3885 date.
- **Contradiction 4:** FRA 1695/1793, plus 3592 and 3593.
- **New 231 in rev2:** 149 (`041`) + 69 (`031`) + 6 (`027`) + 2 (`025`) + 2 (`020`) + FRA@012 + FRA@015 + AGO@022.
- **Conflict 304:** 6 ballot conflicts + 149 (`009` title) + 149 (`014` date).
- **Approved store after the runs:** 6,109 ballots in 41 votes.

Sensitivity (each switch flipped alone from the baseline):

| Switch | Import 1 | Import 2 |
|---|---|---|
| `NORMALISE_CODE_CASE=0` | invalid 12, new 5,877 | new 232, unchanged 5,424 |
| `YES_AS_Y=1` | invalid 10, new 5,879 | **conflict 305** (ARG Y->A spurious), new 230 |
| `ACCEPT_SLASH_DATE=1` | invalid 5, new 5,884 | new 225, unchanged 5,431 |
| `VOTE_LEVEL_DETAIL_CONTRA=1` | contradiction 151, new 5,731 | new 378, unchanged 5,278 |
| `DUP_FIRST_IS_NEW=0` | duplicate 6, new 5,875 | new 234, unchanged 5,422 |
| Vote-detail conflict counted per vote (not run) | – | conflict 6 rows + 2 vote-level conflicts |

## 6. Open questions for the classification design

1. **Date format:** the brief contradicts itself (`yyyy-mm-dd` in Appendix A, `dd-mm-yyyy` in ING-04). The files use `yyyy-mm-dd` everywhere except 6 `dd/mm/yyyy` rows. Recommendation: accept `yyyy-mm-dd` only, document the decision, and treat the slash rows as invalid. Alternatively, accept both brief formats but still reject slashes.
2. **Detail inconsistency scope:** when one vote's rows disagree on title or date, is the outcome contradiction for the minority rows, all rows of the vote, or the whole vote held back? There is also the related question of which value is "the" vote's details when there is no majority.
3. **Duplicate scope:** are both copies "duplicate", or only the repeat? Does a duplicate that also contradicts a third row count as a duplicate or a contradiction? (No such case exists in these files.)
4. **Precedence when a row qualifies for several outcomes**, e.g. invalid and duplicate. The proposed order is invalid, duplicate, contradiction, conflict, unchanged/new.
5. **Normalisation list (ING-13):** BOM strip, CRLF/LF, upper-casing codes. Should trimming whitespace and collapsing double spaces in titles be added? Never map `Yes`->`Y` or empty->`X`, given the D-06 trap.
6. **Conflict granularity for vote-detail changes** (`009` title, `014` date): one conflict per vote or one per row? And when resolved, does accepting it update details only?
7. **Third import with open conflicts:** report them as conflict again, or recognise "same as pending conflict" without opening a second one? Is re-uploading an identical file allowed at all (file hash check)?
8. **Absence:** should votes or states present in approved data but absent from a later file (`040`; FRN/deu/NZL) be reported informationally? Recommendation: report them, but never delete.
9. **Incomplete votes:** should the import report flag votes with fewer rows than members on the date (`031` with 80/149)? ING-10 says missing states are later "new", so this is a warning, not an error.
10. **Name handling (ING-11):** store the published name as received, or canonical only? Should a name that maps to a different code (e.g. "France" with FRN) be used as a hint in the error message only?
11. **Membership boundary:** is `member_to` inclusive, and is a vote on `member_from` inclusive? It doesn't matter for Q1 2024 but does for the historic file.
12. **File ordering:** rely on upload order or on `published_at`? Both agree here (orig 04-02, rev2 04-20).
13. **Session validation:** check it's an integer and consistent within a vote only. Year-based checks fail on the scanned sheets (D-24).
14. **Historic file:** a separate audit is needed when `assembly_historic.csv` arrives (about 1M rows). This report can't speak to it.
