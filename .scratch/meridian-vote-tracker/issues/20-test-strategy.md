# Set the test strategy

Type: grilling
Status: resolved
Blocked by: 12

## Question

Each slice is done only when its tests pass in CI ([Set the build order](12-build-order.md)). Decide:
- **Test projects and layers**: pure unit tests (classification, scoring); use-case modules against real PostgreSQL and Azurite through Testcontainers (never EF in-memory); API tests through `WebApplicationFactory`; any Angular tests
- **The required integration test**: the brief's 7.2 example as an end-to-end import scenario
- **Fixed tests**: the guard test, the decision race and the publication race (one success, one 409), the role matrix (401/403 for every write endpoint)
- **The timed synthetic ~1M-row import**: how the file is generated to mirror [Audit the historic file](17-historic-audit.md), where it runs (CI or on demand), and how time and peak memory are reported
- **Test data**: committed synthetic CSVs only; database isolation between tests
- **What CI runs** on every push

## Answer

Decided with the user in one grilling round, which also took in notes from [Design Docker Compose and image builds](18-docker-compose.md) and [Shape the Angular app](19-angular-app-structure.md).

1. **Two .NET test projects, xUnit v3, built-in `Assert`**:
   - `Meridian.Domain.Tests`: pure units, with no database. Covers parsing, normalisation, classification and scoring.
   - `Meridian.Integration.Tests`: use-case modules, plus the API through `WebApplicationFactory`, against real PostgreSQL and Azurite (Testcontainers). Same image versions as compose (`postgres:18`, Azurite 3.37.0).

   No FluentAssertions, because of its commercial licence.
2. **A fresh database per test, copied from a migrated template.** One PostgreSQL container per run. Migrations and seeding run once (`MigrateAsync`, with `Seed:MemberStatesPath` pointing at the committed synthetic CSV) into `meridian_template`. Each test gets `CREATE DATABASE … TEMPLATE meridian_template`. Respawn and TRUNCATE are ruled out, because the history guard refuses them by design. The guard is never switched off for tests.
3. **Jobs run on demand in tests**: the same job processor is called directly ("run queued jobs until none are left"), with no waiting on the 1-second check. One test runs the real `BackgroundService`, covering the loop, the heartbeat, and resuming after a crash.
4. **The required integration test** is the brief's 7.2 example through the API: upload → job → report, then outcome counts, approved values and the conflict. Also a `RealData` category: the real Q1 files are imported and must give the counts the audit predicted. These tests skip themselves when `candidate_data/` is missing, so they never run in CI.
5. **Fixed tests that can't be skipped by accident**:
   - **Guard**: a catalog query asserts every table except the four on the allowlist has the guard trigger, then checks UPDATE, DELETE and TRUNCATE are refused.
   - **Role matrix**: generated from the API's endpoint metadata. Every write endpoint is called as nobody (401) and as the wrong role (403), so a new endpoint without a policy fails.
   - **Races** (conflict decision, publication, stale position): two connections released together by a barrier, 20 rounds, each round exactly one success and one 409.
   - **Validation errors**: an API test asserts their keys are camelCase JSON paths.
6. **The ~1M-row performance test**: a deterministic generator (fixed seed) copies the historic file's shape from [Audit the historic file](17-historic-audit.md): 6,480 interleaved votes of 147–151 rows, with a scattering of each defect kind. It sits in the `Performance` category. Run locally on demand, it fails above 5 minutes and reports time and peak memory. A manual GitHub workflow reports the numbers without failing. It never runs on every push.
7. **Frontend tests**:
   - Vitest with Angular Material test harnesses.
   - `HttpTestingController` tests for the user interceptor, the mapping from ProblemDetails to form fields, the 409 cards and stopping polling.
   - **One Playwright smoke test** against the compose stack: switch user, upload a small file, see the report, then a decision race in two tabs.
8. **Test data**: committed synthetic files only:
   - `tests/fixtures/` holds one small CSV per import rule.
   - `tests/data/member_states.csv` holds synthetic member states, used by the integration tests and the CI compose job.
   - Unit tests use small C# builders.

   Real data is never committed.
9. **Test-first rules**: classification and scoring are written test-first from the numbered rules in [Define import classification rules](08-import-classification.md) and [Decide the scoring rules](05-scoring-rules.md). Each rule gets a named test.
10. **CI on every push**:
    - build
    - domain tests and integration tests (Testcontainers on GitHub's Ubuntu runners)
    - Angular lint, tests and build
    - an **OpenAPI file check**: fail when building the API changes the committed `contracts/openapi.json`. The web build generates the orval client from that file, so the client can't drift either.
    - the Playwright smoke test on `docker compose up`

    The performance and real-data tests are excluded.
