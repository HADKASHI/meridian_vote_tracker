# Decisions

## What this is and how to read it

The app imports the Assembly's exports, keeps every value's history and publishes a reproducible Alignment Index. Reasoning: [docs/adr/](docs/adr/); tables: [docs/schema.md](docs/schema.md); terms: [GLOSSARY.md](GLOSSARY.md).

## Key decisions

**Data model and history** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md))
- Country votes, details and positions are append-only revisions under a current pointer, and every import keeps its file and an outcome per row. A database guard refuses UPDATE, DELETE and TRUNCATE. Rejected: a JSON audit log, event sourcing, temporal tables.

**Import rules** ([ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- Report, don't repair: only documented normalisations, each recorded on its row; "Yes" is invalid, not mapped. Changed vote details make one conflict per vote. Rejected: mapping ballot words to letters, a conflict per row.

**Concurrency and roles** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md), [ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- A decision is an insert, one per conflict: the loser gets a 409 naming the winner and is kept as "not applied", rather than last write wins. Identity is a demo header; roles are enforced.

**Scoring rules** ([ADR 0006](docs/adr/0006-scoring-rules.md), [ADR 0007](docs/adr/0007-rule-sets-and-evidence-metrics.md))
- Abstentions, absences and non-member votes don't count by default; nothing countable is n/a, not 0; integer weights keep every score checkable by hand. Rejected: half credit, a minimum-vote threshold, floating-point scores.
- Named rule sets vary only abstentions and absences. Each score carries evidence metrics (completeness, abstention rate, missing data) and a High/Medium/Low confidence band that flags thin evidence without reordering. Rejected: letting confidence reorder the ranking.

**Snapshots and publication** ([ADR 0004](docs/adr/0004-snapshots-store-their-answer.md))
- A snapshot stores its answer (each country vote's revision and treatment), so opening it recalculates nothing. A vote without a position blocks it. Rejected: storing only inputs. Publication is per quarter and insert-only.
- Drift and comparison recalculate nothing: a snapshot lists what would differ if created now, and a comparison explains each state's movement by the votes behind it.

**Background jobs and files** ([ADR 0003](docs/adr/0003-job-table-queue-and-blob-files.md), [ADR 0009](docs/adr/0009-azure-run-postgres-versions-and-downloads.md))
- The job table is the queue (SKIP LOCKED, heartbeat, retries, resume). Rejected: Hangfire, a broker, LISTEN/NOTIFY.

**Data access** ([ADR 0005](docs/adr/0005-ef-core-rules-in-csharp.md))
- EF Core only: race-sensitive writes live in use-case modules that own their transaction and lock, and classification and scoring are pure C# functions over one consistent read. Rejected: a generic repository, scoring in SQL, and binary COPY (faster, but the simpler EF writer was kept).

**Azure resources and scanned sheets** ([ADR 0008](docs/adr/0008-pdf-extraction-through-review.md), [ADR 0009](docs/adr/0009-azure-run-postgres-versions-and-downloads.md))
- Used: Blob Storage for source files, Document Intelligence's layout model for scanned sheets, Azure Database for PostgreSQL by a compose override, and Application Insights when configured; Blob and Document Intelligence were checked live. Rejected: Cosmos DB (the history needs relational constraints and triggers), a language model as the sheet reader (its output can't be repeated, and it can invent a mark), and Key Vault (secrets stay in `.env` for now).
- A C# mapper reads the grid; printed totals must match before an analyst approves. Quality would be measured on hand-verified sheets: cell-level country-vote accuracy, header exact matches, the share of sheets matching their printed totals, and how often analysts edit rows.

## Build order and why

A walking skeleton first (whole schema, authorization, the API contract returning 501, CI), then the data's own order: import and scale, conflicts, timeline, positions, snapshots and publication. Each step needs the one before, so every stop leaves a working product; extras and the PDF bonus came last.

## Quality

Tests use real PostgreSQL and Azurite. Generated guard and role tests cover every table and write endpoint, races use two real connections, and a browser smoke test runs in CI. One trace id follows an upload through API and worker; logs hold no client data.

## Not done and known limitations

- The historic-sized import takes about 7 minutes, over the 5-minute target, by decision.
- The Azure database and Application Insights were never run against Azure. The real-data tests for the revision and historic files exist but haven't been run; the original Q1 counts were checked by hand.
- Not built: real sign-in, cancelling a queued import, push progress, direct upload to Blob, unpublishing without a replacement, a non-owner database role.

## Four developers

A shared skeleton ships on day one: the whole schema, authorization, the OpenAPI contract returning 501, the generated client and the Angular shell. Four owners then replace 501s end to end, each slice shipping alone: A import and scale; B conflicts and Review; C votes, timeline and positions; D snapshots and publication. Fixture builders seed each slice's data, so B builds review before A's importer exists.

## Standards to set first

**Contract first** (changes go through the committed OpenAPI file); **migrations** (one in flight; guarded tables only gain columns; new tables are guarded); **definition of done** (API, screen and tests ship together); **error contract** (typed ProblemDetails); **data handling** (no client data in git, logs or fixtures).
