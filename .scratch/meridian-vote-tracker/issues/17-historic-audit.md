# Audit the historic file

Type: research
Status: claimed
Blocked by: 13

## Question

Run the [Audit the real data](07-data-audit.md) method (and its scripts under `research/07-data-audit-scripts/`, extended as needed) over `data_candidate/assembly_historic.csv` (962,171 rows, SHA-256 in [Bring in the historic file](13-historic-file.md)). Which of the 25 known defect classes occur, how often, and are there new ones? Does it overlap the Q1 exports (same vote ids, same or different values), and what would an import of it into an empty system and after the Q1 files produce under the rules in [Define import classification rules](08-import-classification.md)? Flag anything those rules or the schema don't yet handle, for [Lock the database schema](16-lock-database-schema.md). Also measure its shape for the 5-minute performance budget (rows per vote, votes per file) so the synthetic benchmark file can mirror it.
