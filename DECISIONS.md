# Decisions

## What this is and how to read it

A reproducible Alignment Index over the Assembly's messy exports. Reasoning: [docs/adr/](docs/adr/); tables: [docs/schema.md](docs/schema.md); terms: [GLOSSARY.md](GLOSSARY.md).

## Key decisions

**Database and history** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md))
- Values are append-only revisions under a current pointer, guarded against UPDATE, DELETE and TRUNCATE; every import keeps its file and an outcome per row. Rejected: a JSON audit log, event sourcing, temporal tables.

**Import rules** ([ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- Report, don't repair: only documented normalisations, each recorded on its row; changed vote details make one conflict per vote. Rejected: mapping ballot words, a conflict per row.

**Concurrency and roles** ([ADR 0001](docs/adr/0001-typed-append-only-revisions.md), [ADR 0002](docs/adr/0002-import-classification-and-conflicts.md))
- One decision per conflict: the loser gets a 409 naming the winner and is kept as "not applied". Roles are enforced in the API.

**Scoring rules** ([ADR 0006](docs/adr/0006-scoring-rules.md), [ADR 0007](docs/adr/0007-rule-sets-and-evidence-metrics.md))
- Abstentions, absences and non-member votes don't count by default; nothing countable is n/a; integer weights keep scores checkable by hand. Rejected as the default: half credit, a minimum-vote threshold.
- **Alignment rule sets**: the admin can score a snapshot under a named, immutable alternative (abstain as miss, half credit, strict). Every score carries **evidence metrics** (completeness, abstention rate, missing data) and a High/Medium/Low confidence band that flags thin evidence without reordering.

**Snapshots and publication** ([ADR 0004](docs/adr/0004-snapshots-store-their-answer.md))
- A snapshot stores its answer (each country vote's revision and treatment), so opening it recalculates nothing. A vote without a position blocks it. Rejected: storing only inputs.
- **Snapshot drift** lists what would differ if a snapshot were created now; **comparison** explains each state's movement by the votes behind it. Both are reads, so the stored answer never moves.

**Jobs, files and downloads** ([ADR 0003](docs/adr/0003-job-table-queue-and-blob-files.md), [ADR 0009](docs/adr/0009-azure-run-postgres-versions-and-downloads.md))
- The job table is the queue (SKIP LOCKED, heartbeat, retries). Rejected: Hangfire, a broker. Original files download only through the API's role checks.

**Data access** ([ADR 0005](docs/adr/0005-ef-core-rules-in-csharp.md))
- EF Core only: race-sensitive writes live in use-case modules owning their transaction and lock; classification and scoring are pure C# over one consistent read. Rejected: a generic repository, scoring in SQL, binary COPY (faster, but EF was kept).

**Azure resources and scanned sheets** ([ADR 0008](docs/adr/0008-pdf-extraction-through-review.md), [ADR 0009](docs/adr/0009-azure-run-postgres-versions-and-downloads.md))
- Used: Blob Storage for uploaded files and Document Intelligence's layout model for sheets, both checked live; Azure Database for PostgreSQL (17 or 18) by a compose override, and Application Insights when configured. Not used: Azure OpenAI and Claude (a model's output can't be repeated and can invent a mark), Cosmos DB (the history needs relational constraints and triggers), Key Vault (secrets stay in `.env` for now).
- A C# mapper reads the grid, and printed totals must match before an analyst approves. I tested extraction quality by hand against the assignment's scanned sheets. At scale I would measure on a hand-verified set: per-cell accuracy, header exact matches, the share of sheets matching their totals, and how often analysts edit rows.

## Build order and why

A walking skeleton (whole schema, authorization, the API contract returning 501, CI), then the data's order: import, conflicts, timeline, positions, snapshots. Every stop leaves a working product; extras and the PDF bonus came last.

## Quality

Tests use real PostgreSQL and Azurite; races use two real connections; a browser smoke test runs in CI. One trace id follows a request through API, worker and Document Intelligence.

## Before production

Security and authentication first: real sign-in (Entra ID with JWT behind the same policies, replacing the demo header), a least-privilege database role instead of the owner, and secrets in Key Vault reached through a managed identity. Then scale: parallel imports (the per-vote lock keeps data safe; only upload order needs a new rule) and uploads parsed as a stream, not held in memory.

## Four developers

The next phase, one owner each. **A, proactive drift alerts:** notify analysts when approved data moves a published index significantly, with a sandbox to simulate replacing it. **B, verifiable press kits:** a publication journalists can access and verify, without internal files. **C, break-glass history editing:** an authorised, heavily audited override to correct history. **D, scoring suggestions:** suggest the soundest rule set from the data's health (absences, contradictions). As in this phase: the contract first with endpoints returning 501, and fixture builders so nobody waits.

## Standards to set first

1. **Logic lives in the backend**, behind the committed OpenAPI contract; the client is generated.
2. **Every action passes authorization middleware**, proven by a generated role matrix.
3. **History is append-only:** a correction is a new record with who, when and why.
4. **One error contract:** typed ProblemDetails with a trace id.
5. **Done means tested against real PostgreSQL**, with **no client data** in git, logs or fixtures.
