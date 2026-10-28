# Map: Meridian Vote Tracker home assignment

Labels: wayfinder:map

## Destination

A locked build plan for the KPMG "Meridian Vote Tracker" home assignment (Senior .NET Full-Stack Lead): architecture, data model and history design, every rule the brief leaves open, Azure resource choices and build order, settled well enough that building is pure execution and DECISIONS.md is mostly written. Building happens after this map, via `/to-spec` → `/to-tickets` → implementation.

## Notes

- **Source of truth**: the assignment brief and its Appendix B (example screens). Requirement IDs (ING-xx, CON-xx, HIS-xx, POS-xx, SNP-xx, PDF-xx) are cited in every ticket they touch. The brief is in Hebrew; all repo artefacts are written in English.
- **Bar**: correct first, impressive second. Every requirement must be met and traceable; a partial solution with clear reasoning beats a complete shallow one (brief, "Before you start").
- **Stack (fixed by brief)**: ASP.NET Core (8+), PostgreSQL, Angular, Docker Compose. Azure resources are optional and used only through configuration.
- **Data**: the `data_candidate/` files stay out of git and out of git history. They live in a gitignored `data_candidate/` folder at the repo root, mounted read-only by docker compose. Tests use committed synthetic CSVs.
- **Data is per container**: `data_candidate/` does not survive a new session's container. A ticket that reads real data first checks the files are there (and the historic file's SHA-256 against [Bring in the historic file](issues/13-historic-file.md)); if not, it asks the user to upload them zipped through chat before starting.
- **Secrets**: never in code or git; local `.env` or user-secrets only.
- **Skills**: grilling and domain-modeling for every grilling ticket; research for research tickets; prototype where behaviour or UI is the question.
- **Vocabulary**: use `GLOSSARY.md` terms.

## Decisions so far

- [Place the data files locally](issues/01-place-data-files.md): `data_candidate/` is gitignored and holds member_states, both Q1 exports and 2 of 6 voting sheets; the historic file is still missing.
- [Choose current versions and tooling](issues/02-versions-and-tooling.md): .NET 10 LTS, built-in OpenAPI 3.1 + ProblemDetails, orval for the typed Angular client, Angular 22, PostgreSQL 18.
- [Set up the dev environment](issues/03-dev-environment.md): .NET 10 SDK, Node 24 and Docker work in the cloud session via `.claude/cloud-dev-setup.sh`; container builds here need a sandbox-only proxy override.
- [Audit the real data](issues/07-data-audit.md): 25 defect classes catalogued with predicted import counts; files use yyyy-mm-dd; rev2 drops vote 040 and re-sends never-approved values as A; historic file not audited here (see its own ticket).
- [Design the data model and history](issues/04-data-model-and-history.md): keep each file plus a record for every row; every approved value has one origin; conflicts, losing attempts, position changes and snapshots all land on the vote timeline; only new values are stored, and "previous" is derived by the API.
- [Compare history storage patterns](issues/14-compare-history-patterns.md): six options compared on the Brazil scenario; PostgreSQL 18 and EF Core offer no ready-made history for Npgsql; snapshots must reference exact revision ids.
- [Decide the scoring rules](issues/05-scoring-rules.md): only yes/no count; abstain, absent, non-member and "no data" are excluded and labelled; nothing countable scores n/a; exact integers stored; competition ranking; every score shows its basis.
- [Decide concurrency and roles](issues/06-concurrency-and-roles.md): a header user switcher feeds a real ASP.NET auth handler with Analyst/Admin policies (401/403, tested); conflict decisions are conditional updates with a 409 loser; positions are versioned; imports are applied serially, one locked vote per transaction.
- [Choose the history storage pattern](issues/15-choose-history-pattern.md): typed append-only revisions with current pointers; the database refuses updates to history; one insert-only decision per conflict.
- [Define import classification rules](issues/08-import-classification.md): header-driven parsing; documented normalisations (many day-first date formats, upper-cased codes and ballots, never ballot words); order invalid → contradiction → duplicate → conflict/unchanged/new; disagreeing vote details hold back the whole vote; one details conflict per vote; conflicts link to the revision they were compared against; every details decision recalculates membership.
- [Choose how data is read and written](issues/09-data-access.md): EF Core only, with code-first migrations (guard and views as SQL inside them) run by a one-shot compose `migrate` service; EF writes imports behind an `ImportWriter` seam (COPY only if the historic import misses 5 minutes); snapshots stream facts from SQL and score in pure C#; vote rows are locked with `FOR UPDATE`; use-case modules, no generic repository.
- [Design background jobs and live progress](issues/10-background-jobs.md): the PostgreSQL job table is the queue (SKIP LOCKED, checked every second) in a separate `worker` container; imports run one at a time by a setting, not a constraint; heartbeats, retries and resume after a crash; progress polled; files in Azure Blob (Azurite locally); "job" is a glossary term users see.
- [Design snapshots](issues/11-snapshots.md): a snapshot stores its votes, a categorised line per vote and state with the exact revision used, and results, never recomputed; selection by date range, sessions or list; a vote with no position blocks ("Remove these votes" is the way out); a preview warns about pending conflicts, incomplete votes and a running import; the admin publishes per chosen reporting quarter and may replace it with a reason.
- [Bring in the historic file](issues/13-historic-file.md): uploaded zipped through chat into gitignored `data_candidate/`; 962,171 rows, checksum recorded; container reclaims wipe `data_candidate/`, so the other data files need re-uploading here.
- [Audit the historic file](issues/17-historic-audit.md): very clean (62 bad rows in 962,171), no overlap with Q1, so it predicts 962,109 new; 6,480 interleaved votes of about 150 rows; the title column is `resolution_title` (corrects 08); schema flags are handed to the schema ticket.
- [Lock the database schema](issues/16-lock-database-schema.md): full schema in research/16-schema.md. Revisions only added, plus pointers, under a guard trigger; one shared history sequence orders the timeline; unique indexes enforce one decision per conflict, a rejected value never reopening, and one publication chain per quarter; reason and warning kinds are C# enums (no database CHECK).
- [Set the build order](issues/12-build-order.md): six planning tickets first, then a walking skeleton (whole schema as migration 1, auth, generated client, CI); then import → import at scale → conflicts → vote timeline → positions → snapshots → comparison extra → protected finishing pass → PDF bonus, with a half-day limit; one PR per slice, tests inside each slice.
- [Design Docker Compose and image builds](issues/18-docker-compose.md) _(decided by an agent at the user's request)_: one .NET image with three entry points; `migrate` runs the EF bundle and seeds member states, failing loudly without the file; Angular served by nginx with `/api` proxied (same origin); health-checked start order; localhost-only ports; works with no `.env`; sandbox proxy in a separate file; three-job CI with a compose smoke test.
- [Shape the Angular app](issues/19-angular-app-structure.md) _(decided by an agent at the user's request)_: standalone feature folders with lazy routes; Angular Material; signals and `httpResource`, no NgRx; an interceptor sends a per-tab `X-User-Id`; 409s show the current state in place; one `/api/status` poll drives the header jobs indicator; the snapshot preview dialog blocks on votes without a position; split-pane Review inbox (prototype).
- [Set the test strategy](issues/20-test-strategy.md): xUnit v3 domain and integration projects; a fresh database per test copied from a migrated template (the guard rules out TRUNCATE); jobs run on demand; 7.2 through the API, plus local-only real-data counts; guard, role-matrix and race tests generated so none can be skipped; seeded 1M-row test on demand; Vitest plus one Playwright smoke test; CI fails when the OpenAPI file changes.
- [Decide correlation IDs and logging](issues/21-correlation-and-logging.md): W3C trace context started by the server; a job keeps its request's `trace_parent` and runs in the same trace; built-in JSON console logs with scopes, no Serilog; Application Insights only when its connection string is set; logs never carry client data; every error shows its `traceId`; no custom metrics.
- [Outline DECISIONS.md](issues/22-decisions-outline.md): about 1,000 words in seven sections, with full reasoning in six ADRs (`docs/adr/`) created at the start of the build. Four developers build this system in parallel from a deployable skeleton that stubs the whole contract with 501s, each owning vertical slices unblocked by fixtures; five standards to set first; the rewrite happens in the finishing pass.

## Not yet specified

- **Bonus: PDF extraction (section 8)**: Document Intelligence vs LLM vs both, validation against printed totals, the extraction item in the Review inbox, retries and failure surfacing. In scope as a last, optional phase; the Review inbox should already allow other item kinds.

## Out of scope

- [Decide what AI tooling ships with the submission](issues/23-shipped-ai-tooling.md): which skills, `.scratch/` and `.claude/sandbox/` ship, and how AI_USAGE.md tells it. It can only be judged after development, so it is decided in the build's finishing pass, not on this map.
