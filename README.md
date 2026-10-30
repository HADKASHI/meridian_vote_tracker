# Meridian Vote Tracker

A web application for the Northwind Policy Institute's Alignment Index. Analysts upload the Meridian Assembly's CSV exports, and every row gets one recorded outcome with a reason. Disagreements with approved data wait as conflicts for an analyst to decide, and the full history of every value stays visible. Analysts create snapshots that store the Alignment Index they computed, and the admin publishes them.

ASP.NET Core API and background worker on .NET 10, PostgreSQL 18, Azure Blob Storage (Azurite locally), and Angular 22 with Angular Material behind nginx. Everything starts with Docker Compose.

## Run it

You need Docker with Compose v2 and about 6 GB of memory for the engine (the worker is allowed 5 GB so that it can parse the largest accepted file).

1. Put the assignment's `member_states.csv` in `candidate_data/`. The `migrate` step seeds the member states from it, and stops with a message naming the file if it is missing. Everything in that folder except its README is ignored by git and kept out of every image.

   No assignment files at hand? Use the committed synthetic member states instead:

   ```bash
   DATA_DIR=./tests/data docker compose up --build
   ```

2. Start the stack:

   ```bash
   docker compose up --build
   ```

3. Open <http://localhost:8080>.

No `.env` is needed. To change ports, the data folder, the import concurrency or Azure settings, copy `.env.example` to `.env`; every setting is described there. Host ports are bound to `127.0.0.1` only.

The services are `db` (PostgreSQL), `azurite` (Blob emulator), `migrate` (applies the migrations and seeds member states, then exits), `api`, `worker` (runs import and snapshot jobs), and `web` (nginx serving the app and proxying `/api/`).

To stop it, run `docker compose down`. Add `-v` to delete the database and the stored files as well.

## Try it

Upload `assembly_export_2024_q1.csv` on **Imports**, then `assembly_export_2024_q1_rev2.csv`, and follow the screens below. With the synthetic member states, upload the synthetic pair in `e2e/fixtures/` instead: `demo_export_2024_q2.csv`, then `demo_export_2024_q2_rev2.csv`.

- **Imports**: each upload runs as a job with live progress. Its report gives the file's evidence, the count per outcome (new, unchanged, duplicate, invalid, contradiction, conflict), and every row with its reason.
- **Review**: the conflicts the revision opened. Accept the incoming value or keep the current one, with a comment. Open the same conflict as two users in two tabs to see one decision win and the other told who decided first.
- **Votes**: every vote with its country votes, and a timeline of each value's history and where it came from.
- **Positions** (admin sets them): the Institute's stance and weight per vote.
- **Snapshots**: preview a selection (votes without a position block it; pending conflicts, incomplete votes and running imports warn), then create the Alignment Index. The admin may choose a named rule set for how abstentions and absences count. Each score shows its breakdown and its evidence: completeness, abstention rate, missing data, and a High, Medium or Low confidence band that flags thin evidence without changing the rank.
- **Snapshot drift**: a snapshot's page lists what would differ if it were created now (decided conflicts, new imports, changed positions), without recalculating the stored answer.
- **Snapshot comparison**: two snapshots side by side, with each state's rank and score movement and the votes that explain it.
- **Publications**: the admin publishes a snapshot for a quarter, or replaces it.
- **Jobs**: everything queued, running, done or failed. The header shows the same at a glance.

**PDF bonus**: with Azure AI Document Intelligence configured (see `.env.example`), scanned voting sheets are uploaded on Imports. Each sheet is read into rows and checked against its printed totals, then waits on the **Extractions** tab of Review until an analyst fixes or approves it. Approval turns it into an ordinary import.

## Users and roles (demo only)

The header's user switcher picks who you act as, per browser tab: **Dana** and **Omar** are analysts, **Lena** is the admin. The API checks roles on every call.

Identity is a plain `X-User-Id` header, so anyone who knows a user id can act as that user, the admin included. This is a deliberate demo shortcut and must not be used in production. The replacement is real authentication (for example Entra ID with JWT bearer) behind the same policies in `src/Meridian.Platform/Identity/Policies.cs`.

## Tests

The backend needs the .NET 10 SDK and Docker (the integration tests start PostgreSQL and Azurite in Testcontainers):

```bash
dotnet test --solution Meridian.slnx -- --filter-not-trait "Category=RealData" --filter-not-trait "Category=Performance"
```

The frontend needs Node 24:

```bash
cd web && npm ci && npm run lint && npm test
```

The browser smoke test runs against a running stack. It switches user, uploads a small file, reads the report, and races a conflict decision in two tabs:

```bash
cd e2e && npm ci && npx playwright install chromium && npm test
```

Two categories only run when asked, never in CI:

- **RealData** checks the audited counts of the assignment files. Put them in `candidate_data/` first; the tests skip themselves without them.

  ```bash
  dotnet test --project tests/Meridian.Integration.Tests -- --filter-trait "Category=RealData"
  ```

- **Performance** imports a synthetic file of about a million rows, and fails above 5 minutes. It also runs as the manual **Performance** workflow on GitHub Actions.

  ```bash
  dotnet test --project tests/Meridian.Integration.Tests -- --filter-trait "Category=Performance"
  ```

CI runs on every push. It builds with warnings as errors, checks that the committed OpenAPI file and the migrations match the code, and runs the tests on PostgreSQL 18 and 17. It also runs the frontend's lint, tests and build, and the compose stack with the smoke test.

## Running on Azure

An override file runs the app against Azure Database for PostgreSQL and Azure Blob Storage instead of the local database and Azurite. The setup, Entra ID sign-in and a guided live check (`scripts/check-azure.sh`) are in [docs/azure.md](docs/azure.md). PostgreSQL 17 and 18 are both supported ([docs/postgres-versions.md](docs/postgres-versions.md)).

## Where things are

- [DECISIONS.md](DECISIONS.md): the key decisions, the build order, and how four developers would split the work. The full reasoning is in the ADRs in [docs/adr/](docs/adr/).
- [docs/schema.md](docs/schema.md): the database schema. [GLOSSARY.md](GLOSSARY.md): the domain's words, used the same way in the UI, the API and the code.
- [AI_USAGE.md](AI_USAGE.md): how AI was used, and where its output was checked, rejected or corrected.
- `contracts/openapi.json`: the API contract, generated from the code at build time. The Angular client is generated from it.
- `src/`: the API, the worker, and one project per feature module (Imports, Conflicts, Votes, Positions, Snapshots, Extractions) over shared `Domain`, `Data` and `Platform` projects.
- `web/`: the Angular app. `e2e/`: the browser smoke test and screen captures. `tests/`: the domain and integration test projects.
