# Decisions

## What this is and how to read it

Meridian Vote Tracker imports the Assembly's exports, keeps every value's history, lets analysts decide conflicts, and publishes a reproducible Alignment Index. Each decision gives why and what was rejected; full reasoning is in [docs/adr/](docs/adr/), tables in [docs/schema.md](docs/schema.md), terms in [GLOSSARY.md](GLOSSARY.md).

## Key decisions

**Data model and history** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md))
- Country votes, details and positions are append-only revisions under a current pointer, and every import keeps its file and a verdict per row. A database guard refuses UPDATE, DELETE and TRUNCATE. Rejected: a JSON audit log, event sourcing, temporal tables.

**Import rules** ([ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- Report, don't repair: only documented normalisations, each recorded on its row; "Yes" is invalid, not mapped. Changed vote details make one conflict per vote, not per row.

**Concurrency and roles** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md), [ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- A decision is an insert, one per conflict: the loser gets a 409 naming the winner and is kept as "not applied". Imports commit per vote under the same lock. Identity is a demo header; the role policies are real.

**Scoring rules** ([ADR 0006](docs/adr/0006-scoring-rules.md), [ADR 0007](docs/adr/0007-rule-sets-and-evidence-metrics.md))
- By default abstentions, absences and non-member votes are not counted, nothing countable is n/a rather than 0, and integer weights make every score checkable by hand. Named rule sets vary only abstentions and absences.

**Snapshots and publication** ([ADR 0004](docs/adr/0004-snapshots-store-their-answer.md))
- A snapshot stores its answer, every ballot's revision and treatment, so opening it recalculates nothing. Rejected: storing only inputs. A vote without a position blocks it. Publication is per quarter and insert-only.

**Background jobs and files** ([ADR 0003](docs/adr/0003-job-table-queue-and-blob-files.md), [ADR 0009](docs/adr/0009-azure-run-postgres-versions-and-downloads.md))
- The job table is the queue (SKIP LOCKED, heartbeat, retries, resume). Rejected: Hangfire, a broker, LISTEN/NOTIFY. Files live in Blob (Azurite locally) and are downloaded only through the API's role checks.

**Data access** ([ADR 0005](docs/adr/0005-ef-core-rules-in-csharp.md))
- EF Core only. Race-sensitive writes live in use-case modules that own their transaction and lock; reads are projections. Rejected: a generic repository.
- Rules in C#, facts from SQL: classification and scoring are pure, tested functions over one consistent read. Binary COPY met the import budget but was not adopted; the simpler EF writer was kept.

## Build order and why

A walking skeleton first: compose, the whole schema, authorization, the full API contract returning 501, and CI. Then the data's own order: import and its scale, conflicts, the timeline, positions, snapshots and publication. Nothing can be reviewed before it is imported, or scored without positions, so every stop leaves a working product, riskiest parts first. Design polish, extras and the PDF bonus followed, each cut-able.

## Quality

Tests use real PostgreSQL and Azurite, since the guarantees live in triggers, locks and constraints. Generated guard and role tests cover every table and write endpoint, races run on two real connections, and a browser smoke test runs in CI. One trace id follows an upload through API and worker; logs hold no client data.

## Not done and known limitations

- The historic-sized import takes about 7 minutes, over the 5-minute target, by decision; a 200 MB file needs about 3 GiB of worker memory.
- On Azure, Blob and Document Intelligence were checked live; the managed database and Application Insights were not. The real-data counts for the revised and historic files are not yet run.
- Not built: real sign-in, cancelling a queued import, push progress, direct upload to Blob, unpublishing without a replacement, a database overlap constraint on membership, a non-owner database role.

## Four developers

Four people build this system in parallel. First, a shared skeleton deployable on day one: compose, the whole schema as one migration, authorization, the full OpenAPI contract returning 501, the generated client and the Angular shell. Then four owners replace 501s end to end, each slice shipping alone: A import and scale; B conflicts and the Review inbox; C vote page, timeline and positions; D snapshots and publication. Nobody waits: with schema and contract fixed, fixture builders seed each slice's data, so B builds review before A's importer exists.

## Standards to set first

1. **Contract first:** the API changes through the committed OpenAPI file; the generated client is never hand-edited.
2. **Migrations:** one in flight at a time; guarded tables only gain columns, and new tables are guarded unless allowlisted.
3. **Definition of done:** API, screen and tests together, with guard, role and race tests green.
4. **Error contract:** a catalogue of ProblemDetails types the UI maps one to one.
5. **Data handling:** no client data in git, logs or fixtures, and the glossary's words in code, API and UI.
