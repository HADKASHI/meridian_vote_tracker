# Lock the database schema

Type: grilling
Status: resolved
Blocked by: 08, 09, 10, 11, 15, 17

## Question

Turn the draft [data model sketch](../research/15-data-model-sketch.md) into the final schema the build follows: every table and column with its type, keys, indexes, check and unique constraints, the database guard (which tables refuse UPDATE and DELETE), the views used for current values and timelines, and the seed data. It must fold in what [Define import classification rules](08-import-classification.md), [Choose how data is read and written](09-data-access.md), [Design background jobs and live progress](10-background-jobs.md) and [Design snapshots](11-snapshots.md) decide. Output: the final diagram plus a table list, ready for the first migration.

## Comments

- From [Define import classification rules](08-import-classification.md): the schema must hold a conflict's link to the revision it was compared against (current is read live, never copied), conflicts on two subjects (ballot, vote details), several conflict links per row record, a decision as the origin of ballots approved by a membership recalculation, per-row normalisations, reason codes, and file-level warnings and a failed status on the import.
- From [Choose how data is read and written](09-data-access.md): EF code-first migrations with snake_case names (`EFCore.NamingConventions`); trigger, views and partial unique indexes as raw SQL inside the migrations; "current value" views mapped as keyless entities; the vote row is the per-vote lock target (`FOR UPDATE`), so every vote must have a row before its first revision.

- From [Design background jobs and live progress](10-background-jobs.md): add a changeable `job` table (kind, link to an import or snapshot, state queued/running/done/failed, progress counters, `heartbeat_at`, attempts, last error, start and finish times), outside the history guard, with an index serving "oldest queued of kind". There is no unique index limiting running imports: that limit is a setting enforced at claim time. The import keeps the Blob name and SHA-256 of its file.
- From [Design snapshots](11-snapshots.md): `snapshot` header (name, note, selection criteria as entered including removed votes, created by/at, scoring rules version); `snapshot_vote` (vote, details revision, position revision); `snapshot_line` per (vote, state) with country vote revision (nullable = no data) and category; `snapshot_result` per state (matched and counted weight, rank nullable); `snapshot_warning` as stood at calculation time. All insert-only under the guard; contents written in the job's final transaction. `publication` insert-only (snapshot, quarter, published by/at, reason, supersedes publication): unique successor per publication and one unsuperseded publication per quarter (partial unique indexes, or an equivalent the schema ticket chooses).
- From [Audit the historic file](17-historic-audit.md): the required title column is `resolution_title` (08 said `title`). Decide whether rows missing only optional trailing columns are padded (the 11 truncated rows then become duplicates) or stay invalid. Add structured reason codes, including shifted columns that still match the field count, placeholder codes such as `XXX`, and voted before joining. Store `published_at` per row, and define what 08's ordering warning compares, since historic-after-Q1 always triggers it. One import writes ~962k row records: index `(import_id, outcome)` and `(import_id, vote_id)`, or keep counts on the job. Store incomplete-vote warnings as rows. Put no unique index on (title, date): 151 pairs are shared by different votes. Add a unit test for the inclusive membership-boundary day, because no real row falls on one.

## Answer

Decided with the user in two grilling rounds. The final schema (diagram, every table with its columns, keys, constraints and indexes, the views, guard and seed, and the reasons behind its shape) is [research/16-schema.md](../research/16-schema.md). The user overturned one proposal: reason codes and warning kinds were to be checked by the database; instead they are **C# enums stored as text with no database CHECK**. The database checks only values that protect integrity.

1. **Conventions**: `bigint` keys. Natural keys for member states (`char(3)`) and users (slug). Integrity values are `text` + CHECK, not PostgreSQL enums. Every history row has `recorded_at` and an actor. Pointers use composite FKs, so they only reach their own vote's (and state's) revisions.
2. **One shared `history_seq`** for every guarded table's id, so the timeline orders by id across tables.
3. **A vote row is created the first time a well-formed id is seen** (even an all-contradiction vote). Row records point to it with a foreign key. It is the per-vote lock target.
4. **A rejected value never reopens**: conflicts are unique on (compared-against revision, incoming value) per subject, as partial unique indexes. A matching incoming value links to the existing conflict, pending or decided (reason `previously_rejected`).
5. **One `conflict` table** for both subjects (ballot, vote details), with CHECKs on which columns are set. `import_row_conflict` links rows to up to two conflicts. `conflict_decision` is unique per conflict (CON-05).
6. **Origins**: each revision has exactly one of `origin_import_row_id` / `origin_decision_id` (CHECK). A decision records the revision it replaced; the revision it created points back by origin, so the FKs have no cycle. Row records link to the revision they created or matched. Normalisations are a `jsonb` array on the row record.
7. **Cut-short rows stay invalid** (`field_count`). No padding.
8. **Reason codes**: C# enum stored as text, no database CHECK; the readable message is stored as it was at import time.
9. **Warnings**: kinds are a C# enum; records go in insert-only `import_warning` (one row per incomplete vote) and `snapshot_warning`. Facts known only after parsing (header, BOM, line endings, newest `published_at`, rejection reason) go in a one-per-import `import_parse` row, because `import` itself is written at upload time. The older-`published_at` warning compares this file's newest value with the newest from any earlier import.
10. **Row record indexes**: unique `(import_id, line_no)`, `(import_id, outcome)`, `(import_id, vote_id)`, `(vote_id)`. Parsed `published_at` is kept per row. Counts are always queried.
11. **Position concurrency**: the version is the position revision id. The save is a conditional pointer update (`IS NOT DISTINCT FROM @seen`), and zero rows means 409.
12. **Guard** (UPDATE/DELETE per row, TRUNCATE per statement) on every table except `job`, `vote_current`, `country_vote_current` and `__EFMigrationsHistory`. Reference tables are guarded too.
13. **Seed**: `migrate` loads member states from the read-only `candidate_data/member_states.csv` if the table is empty, and fails loudly if the file is missing. Users `dana`, `omar` (analyst) and `lena` (admin) come in a migration. Tests use committed synthetic member states.
14. **Views**: `current_country_vote`, `current_vote`, `pending_conflict`, and a `vote_timeline` `UNION ALL` paged by history id, with "from → to" pairs built in C#.
15. **Publication**: an insert-only chain. Partial unique indexes allow one first publication per quarter and one successor per publication, and a composite FK keeps a replacement in its quarter. A reason is required exactly when one publication supersedes another.
16. **Snapshot results** are checked by the database: `matched ≤ counted`, and rank is null exactly when counted is 0.
