# Decisions

## What this is and how to read it

The app imports the Assembly's exports, keeps every value's history and publishes a reproducible Alignment Index. Reasoning: [docs/adr/](docs/adr/); tables: [docs/schema.md](docs/schema.md); terms: [GLOSSARY.md](GLOSSARY.md).

## Key decisions

**Database and history** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md))
- Country votes, details and positions are append-only revisions under a current pointer; every import keeps its file and an outcome per row. A database guard refuses UPDATE, DELETE and TRUNCATE. Rejected: a JSON audit log, event sourcing, temporal tables.

**Import rules** ([ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- Report, don't repair: only documented normalisations, each recorded on its row; "Yes" is invalid, not mapped. Changed vote details make one conflict per vote. Rejected: mapping ballot words, a conflict per row.

**Concurrency and roles** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md), [ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- A decision is an insert, one per conflict: the loser gets a 409 naming the winner and is kept as "not applied". Identity is a demo header; roles are enforced in the API.

**Scoring rules** ([ADR 0006](docs/adr/0006-scoring-rules.md), [ADR 0007](docs/adr/0007-rule-sets-and-evidence-metrics.md))
- Abstentions, absences and non-member votes don't count by default; nothing countable is n/a, not 0; integer weights keep every score checkable by hand. Rejected as the default: half credit, a minimum-vote threshold.
- **Alignment rule sets**: the admin can score a snapshot under a named, immutable alternative (abstain as miss, half credit, strict). Every score carries **evidence metrics** (completeness, abstention rate, missing data) and a High/Medium/Low confidence band that flags thin evidence without reordering.

**Snapshots and publication** ([ADR 0004](docs/adr/0004-snapshots-store-their-answer.md))
- A snapshot stores its answer (each country vote's revision and treatment), so opening it recalculates nothing. A vote without a position blocks it. Publication is per quarter and insert-only. Rejected: storing only inputs.
- **Snapshot drift** lists what would differ if a snapshot were created now; **comparison** explains each state's movement by the votes behind it. Both are reads, so the stored answer never moves.

**Jobs, files and downloads** ([ADR 0003](docs/adr/0003-job-table-queue-and-blob-files.md), [ADR 0009](docs/adr/0009-azure-run-postgres-versions-and-downloads.md))
- The job table is the queue (SKIP LOCKED, heartbeat, retries, resume). Rejected: Hangfire, a broker. Original CSVs and sheet PDFs download only through the API's role checks.

**Data access** ([ADR 0005](docs/adr/0005-ef-core-rules-in-csharp.md))
- EF Core only: race-sensitive writes live in use-case modules owning their transaction and lock; classification and scoring are pure C# over one consistent read. Rejected: a generic repository, scoring in SQL, binary COPY (faster, but EF was kept).

**Azure resources and scanned sheets** ([ADR 0008](docs/adr/0008-pdf-extraction-through-review.md), [ADR 0009](docs/adr/0009-azure-run-postgres-versions-and-downloads.md))
- Used: Blob Storage for uploaded files and Document Intelligence's layout model for sheets, both checked live; Azure Database for PostgreSQL (17 or 18) by a compose override, and Application Insights when configured. Not used: Azure OpenAI and Claude (a model's output can't be repeated and can invent a mark), Cosmos DB (the history needs relational constraints and triggers), Key Vault (secrets stay in `.env` for now).
- A C# mapper reads the grid, and printed totals must match before an analyst approves. Quality would be measured on hand-verified sheets: per-cell accuracy, header exact matches, the share of sheets matching their totals, and how often analysts edit rows.

## Build order and why

A walking skeleton first (whole schema, authorization, the API contract returning 501, CI), then the data's order: import and scale, conflicts, timeline, positions, snapshots and publication. Each step needs the one before, so every stop leaves a working product; the extras and the PDF bonus came last.

## Quality

Tests use real PostgreSQL and Azurite. Generated guard and role tests cover every table and write endpoint, races use two real connections, and a browser smoke test runs in CI. One trace id follows a request through API, worker and Document Intelligence; logs hold no client data.

## Before production

Real sign-in (Entra ID behind the same policies), a non-owner database role, secrets in Key Vault through a managed identity, the database and Application Insights run on Azure, and the COPY writer: the historic-sized import takes about 7 minutes against a 5-minute target. Not built: cancelling a queued import, push progress, direct upload to Blob, unpublishing without a replacement.

## Four developers

Next phase, one owner each: A production platform (sign-in, Key Vault, deployment, telemetry); B ingestion at scale (COPY, direct upload, cancelling); C review and sheets (sheet image beside its rows, the quality set above); D scoring and publication (unpublishing, new rule sets). This phase was cut the same way: a day-one skeleton with the whole schema and every endpoint returning 501, then four owners replacing 501s, with fixture builders so nobody waits.

## Standards to set first

**Contract first** (changes go through the committed OpenAPI file); **migrations** (one in flight; guarded tables only gain columns; new tables are guarded); **definition of done** (API, screen and tests ship together); **error contract** (typed ProblemDetails); **data handling** (no client data in git, logs or fixtures).
