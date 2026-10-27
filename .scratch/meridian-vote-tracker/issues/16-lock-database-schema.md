# Lock the database schema

Type: grilling
Status: open
Blocked by: 08, 09, 10, 11, 15

## Question

Turn the draft [data model sketch](../research/15-data-model-sketch.md) into the final schema the build follows: every table and column with its type, keys, indexes, check and unique constraints, the database guard (which tables refuse UPDATE and DELETE), the views used for current values and timelines, and the seed data. It must fold in what [Define import classification rules](08-import-classification.md), [Choose how data is read and written](09-data-access.md), [Design background jobs and live progress](10-background-jobs.md) and [Design snapshots](11-snapshots.md) decide. Output: the final diagram plus a table list, ready for the first migration.

## Comments

- From [Define import classification rules](08-import-classification.md): the schema must hold a conflict's link to the revision it was compared against (current is read live, never copied), conflicts on two subjects (ballot, vote details), several conflict links per row record, a decision as the origin of ballots approved by a membership recalculation, per-row normalisations, reason codes, and file-level warnings and a failed status on the import.
- From [Choose how data is read and written](09-data-access.md): EF code-first migrations with snake_case names (`EFCore.NamingConventions`); trigger, views and partial unique indexes as raw SQL inside the migrations; "current value" views mapped as keyless entities; the vote row is the per-vote lock target (`FOR UPDATE`), so every vote must have a row before its first revision.
