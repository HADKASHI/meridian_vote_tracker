# Audit the real data

Type: research
Status: claimed
Blocked by: 01

## Question

Inspect every file in `data_candidate/` and list each defect class it contains, with counts and example row numbers: malformed CSV, wrong column counts, missing fields, unknown or lower-case country codes, invalid ballots, date formats, non-member votes, in-file duplicates and contradictions, inconsistent vote details, name variants for one code (Turkey/Türkiye), split votes, and what rev2 changes relative to the Q1 file. Note the brief's date-format inconsistency (ING-04 says dd-mm-yyyy, Appendix A says yyyy-mm-dd) and what the files actually use.
