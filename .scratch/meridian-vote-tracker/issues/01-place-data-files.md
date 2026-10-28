# Place the data files locally

Type: task
Status: resolved

## Question

Get the `data_candidate/` files (member_states.csv, assembly_export_2024_q1.csv, assembly_export_2024_q1_rev2.csv, assembly_historic.csv, voting_sheets/) into a gitignored `data_candidate/` folder at the repo root without them ever entering git history. HITL: the user uploads them, the agent adds the .gitignore entry, copies them in, and checks `git status` shows nothing to commit. Record file sizes and row counts for later tickets.

## Answer

`data_candidate/` at the repo root is ignored by `.gitignore` (`/data_candidate/`); `git check-ignore` confirms it, and `git status` shows nothing from it to commit. The files must be uploaded again in each new cloud session.

| File | Bytes | Lines (incl. header) | sha256 (first 16) |
| --- | --- | --- | --- |
| member_states.csv | 4,003 | 152 | 0a5c5779d7ac9254 |
| assembly_export_2024_q1.csv | 625,955 | 5,897 | 947b1013bae949c4 |
| assembly_export_2024_q1_rev2.csv | 644,386 | 5,961 | 008e7fb1ca666413 |
| voting_sheets/MA_2007_015.pdf | 1,251,381 | n/a | 3e7985f422cba461 |
| voting_sheets/MA_2009_117.pdf | 1,746,746 | n/a | 7a432468fb657aa4 |

Missing: `assembly_historic.csv` (~100MB, too big for chat upload) and 4 of the 6 voting sheets. Getting the historic file in is now its own ticket, [Bring in the historic file](13-historic-file.md).

Facts for later tickets: member_states has 151 rows for 150 states (Iceland left 2019-12-31 and rejoined 2022-01-01). New Zealand's membership ended 2021-06-30. Montenegro joined 2007, South Sudan 2016-03-01, Timor-Leste 2023-01-01. Türkiye is listed under that name.
