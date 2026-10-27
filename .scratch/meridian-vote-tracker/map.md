# Map: Meridian Vote Tracker home assignment

Labels: wayfinder:map

## Destination

A locked build plan for the KPMG "Meridian Vote Tracker" home assignment (Senior .NET Full-Stack Lead): architecture, data model and history design, every rule the brief leaves open, Azure resource choices and build order, settled well enough that building is pure execution and DECISIONS.md is mostly written. Building happens after this map, via `/to-spec` → `/to-tickets` → implementation.

## Notes

- **Source of truth**: the assignment brief and its Appendix B (example screens). Requirement IDs (ING-xx, CON-xx, HIS-xx, POS-xx, SNP-xx, PDF-xx) are cited in every ticket they touch. The brief is in Hebrew; all repo artefacts are written in English.
- **Bar**: correct first, impressive second. Every requirement must be met and traceable; a partial solution with clear reasoning beats a complete shallow one (brief, "Before you start").
- **Stack (fixed by brief)**: ASP.NET Core (8+), PostgreSQL, Angular, Docker Compose. Azure resources are optional and used only through configuration.
- **Data**: the `data_candidate/` files stay out of git and out of git history. They live in a gitignored `data_candidate/` folder at the repo root, mounted read-only by docker compose. Tests use committed synthetic CSVs.
- **Secrets**: never in code or git; local `.env` or user-secrets only.
- **Skills**: grilling and domain-modeling for every grilling ticket; research for research tickets; prototype where behaviour or UI is the question.
- **Vocabulary**: use `GLOSSARY.md` terms.

## Decisions so far

- [Place the data files locally](issues/01-place-data-files.md): `data_candidate/` is gitignored and holds member_states, both Q1 exports and 2 of 6 voting sheets; the historic file is still missing.
- [Choose current versions and tooling](issues/02-versions-and-tooling.md): .NET 10 LTS, built-in OpenAPI 3.1 + ProblemDetails, orval for the typed Angular client, Angular 22, PostgreSQL 18.
- [Set up the dev environment](issues/03-dev-environment.md): .NET 10 SDK, Node 24 and Docker work in the cloud session via `.claude/cloud-dev-setup.sh`; container builds here need a sandbox-only proxy override.
- [Audit the real data](issues/07-data-audit.md): 25 defect classes catalogued with predicted import counts; files use yyyy-mm-dd; rev2 drops vote 040 and re-sends never-approved values as A; historic file not yet audited.
- [Design the data model and history](issues/04-data-model-and-history.md): keep each file plus a record for every row; every approved value has one origin; conflicts, losing attempts, position changes and snapshots all land on the vote timeline; only new values are stored, and "previous" is derived by the API.
- [Compare history storage patterns](issues/14-compare-history-patterns.md): six options compared on the Brazil scenario; PostgreSQL 18 and EF Core offer no ready-made history for Npgsql; snapshots must reference exact revision ids.
- [Decide the scoring rules](issues/05-scoring-rules.md): only yes/no count; abstain, absent, non-member and "no data" are excluded and labelled; nothing countable scores n/a; exact integers stored; competition ranking; every score shows its basis.
- [Decide concurrency and roles](issues/06-concurrency-and-roles.md): a header user switcher feeds a real ASP.NET auth handler with Analyst/Admin policies (401/403, tested); conflict decisions are conditional updates with a 409 loser; positions are versioned; imports are applied serially, one locked vote per transaction.
- [Choose the history storage pattern](issues/15-choose-history-pattern.md): typed append-only revisions with current pointers; the database refuses updates to history; one insert-only decision per conflict.

## Not yet specified

- **Bonus: PDF extraction (section 8)**: Document Intelligence vs LLM vs both, validation against printed totals, the extraction item in the Review inbox, retries and failure surfacing. In scope as a last, optional phase; the Review inbox should already allow other item kinds.
- **Correlation IDs end to end**: from the Angular request through the API and background jobs to AI calls; logging shape; Application Insights or local only.
- **Angular app structure**: routing for the four screens, the user switcher, field-level validation errors from the API error format, presenting a 409 for concurrent decisions.
- **Test strategy**: beyond the required integration test against real PostgreSQL. Likely the brief's 7.2 example as an end-to-end import scenario, scoring unit tests, and a concurrency test.
- **DECISIONS.md outline**: the 2-page limit, what goes in, the plan for splitting the next phase across four developers, and which standards to set first.
- **Docker Compose and image builds**: services (db, api, web, maybe a worker), seeding member states and users on `up`, mounting `data_candidate/` read-only, configuration for Azure resources, and a sandbox-only override for the proxy.
- **Trim the skills folder before submission**: keep only the skills this project uses; record the AI workflow in AI_USAGE.md.

## Out of scope
