# Set the build order

Type: grilling
Status: resolved
Blocked by: 02, 03, 04, 05, 06, 08, 09, 10, 11, 15, 16

## Question

In what order are the vertical slices built, so that a partial submission is still coherent and the order itself shows good prioritisation (the brief says the order is assessed)? What extras make it stand out without risking correctness, and where is the line for the bonus?

## Comments

- From [Design background jobs and live progress](10-background-jobs.md): compose gains `worker` and `azurite` services, and the integration tests need Azurite via Testcontainers. The Jobs list and the header indicator are UI to schedule.
- From [Design snapshots](11-snapshots.md): candidate extra: comparing two snapshots (rank movement between quarters, or a publication and the one it superseded), a pure read over stored snapshot lines. The create dialog has a preview step (vote count, blocking "no position" list with "Remove these votes", warnings).
- From [Lock the database schema](16-lock-database-schema.md): the first migration is fully specified in [research/16-schema.md](../research/16-schema.md), including tables, guard trigger, views and seeding by the `migrate` service from `data_candidate/member_states.csv`. The guard test, the decision-race test (unique index → 409) and the publication-race test are early, cheap wins that show integrity. The "Why this shape" section is ready-made material for DECISIONS.md.

## Answer

Decided with the user in one grilling round. The user dropped the time budget (Q10): no day plan. The user takes responsibility for time, so only the order and the cut rule stand.

**Before building**, settle the planning tickets this answer created:
- [Design Docker Compose and image builds](18-docker-compose.md), [Shape the Angular app](19-angular-app-structure.md), [Set the test strategy](20-test-strategy.md) and [Decide correlation IDs and logging](21-correlation-and-logging.md) come before the skeleton.
- [Outline DECISIONS.md](22-decisions-outline.md) and [Decide what AI tooling ships with the submission](23-shipped-ai-tooling.md) come before the finishing pass.

**Order** (each step delivers API, worker, screen and tests together, and is done only when its tests pass in CI):

0. **Walking skeleton**:
   - `docker compose up` on a clean clone runs db, migrate, api, worker, web and azurite
   - the **whole locked schema as migration 1** ([research/16-schema.md](../research/16-schema.md)), with the guard test
   - the user switcher feeding the real auth handler, with Analyst and Admin policies
   - the OpenAPI document and the generated orval client, called from one Angular page
   - the CI workflow

   Integration risk is removed before any feature work.
1. **Import** (ING): upload → job → classify → `ImportWriter` → report screen, with live progress. The brief's 7.2 example is the required integration test against real PostgreSQL.
2. **Import at scale**: the timed synthetic ~1M-row import (5 minutes, peak memory). On a miss, `ImportWriter` switches to COPY now, while that's cheap.
3. **Conflicts** (CON): Review inbox, accept or reject with a comment, membership recalculation, and the decision race test (one 200, one 409).
4. **Vote page and timeline** (HIS): it shows everything slices 1–3 produce.
5. **Positions and weights** (POS): admin only, with a 409 on a stale save.
6. **Snapshots and publication** (SNP): preview and blocking, the job, breakdown, publish and replace, and the publication race test.
7. **Extra: snapshot comparison** (rank movement between two snapshots, a read over stored lines). The only feature extra; CI is the other, inside the skeleton.
8. **Finishing pass** (protected): `docker compose up` on a fresh clone, README, DECISIONS.md trimmed to 2 pages, AI_USAGE.md, and the skills trim.
9. **Bonus: PDF extraction** (section 8): only once 0–8 are done, with a hard half-day limit. Otherwise DECISIONS.md describes its design.

**Why this order**: it follows the data (nothing can be reviewed before it's imported, nothing scored without positions), so stopping after any slice leaves a coherent product. The riskiest parts, ingestion correctness and scale, are proven first.

**Cut rule**: if time runs short, the bonus goes first, then the comparison extra. The finishing pass is never cut.

**Working rules**:
- Tests belong to their slice. There is no separate testing phase.
- Every slice ships its screen. There is no backend-first phase.
- One branch and PR per slice, merged into `main`, so the history shows the build order. Each PR lists the requirement IDs it covers.
- Later slices add no schema migrations unless something was missed.
- From [Outline DECISIONS.md](22-decisions-outline.md): the first build step moves today's DECISIONS.md text into six ADRs in `docs/adr/` and `research/16-schema.md` into `docs/schema.md`; later slices add reasoning to ADRs, not to DECISIONS.md. The four-developer plan uses a skeleton that stubs the **whole** contract with 501s, so each slice is a story that replaces stubs. The user's own build can do the same in its skeleton ("just like we would do it here").
