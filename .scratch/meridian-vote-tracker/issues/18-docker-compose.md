# Design Docker Compose and image builds

Type: grilling
Status: resolved
Blocked by: 12

## Question

What exactly does `docker compose up` on a clean clone run, and how are the images built? Settle:
- **Services**: `db`, `migrate`, `api`, `worker`, `web`, `azurite`
- **Images**: one .NET image with two entry points (`api`, `worker`) or two images; the EF migration bundle for `migrate`; the Angular build served by nginx or by the API
- **Startup order and health checks**: `migrate` completes successfully before `api` and `worker` start; `db` and `azurite` are healthy first
- **Seeding**: users in a migration; member states from the read-only `data_candidate/` mount, failing loudly if the file is missing (see [Lock the database schema](16-lock-database-schema.md))
- **Configuration**: `.env` and user-secrets, Blob connection string (Azurite by default, real Azure optional), ports
- **Sandbox**: the proxy override this cloud session needs (see [Set up the dev environment](03-dev-environment.md)), kept out of the reviewer's path
- **CI**: what CI builds and runs

Needed before the walking skeleton in [Set the build order](12-build-order.md).

## Answer

Decided by an agent at the user's request (the user delegated this ticket). Image tags, the Dockerfile, compose, nginx and CI sketches, the sandbox override and every rejected alternative are in [research/18-docker-compose.md](../research/18-docker-compose.md).

1. **Six services** in one `compose.yaml` at the repo root, project `name: meridian`: `db` (`postgres:18`), `azurite` (`mcr.microsoft.com/azure-storage/azurite:3.37.0`, Blob only, `--skipApiVersionCheck` so a newer Blob SDK isn't refused), `migrate` (one-shot), `api`, `worker`, `web`. Floating tags within a fixed major or minor (`postgres:18`, `dotnet/sdk:10.0`, `dotnet/aspnet:10.0`, `node:24-alpine`, `nginx:1.30-alpine`), so security patches flow in; only Azurite is pinned exactly, because its version decides which Blob API versions it accepts.
2. **One .NET image, three entry points.** One multi-stage `src/Dockerfile` (SDK build stage, `aspnet:10.0` runtime) publishes `/app/api`, `/app/worker` and the migration bundle `/app/migrate/efbundle`. `migrate`, `api` and `worker` share one `build` + `image: meridian-backend:local` (a YAML anchor) and differ only in `entrypoint`, so the code can't differ between them. It runs as the image's non-root `app` user. Rejected: separate images per service; one host with a role flag; chiseled and Alpine bases.
3. **`migrate` runs the EF migration bundle**, framework-dependent and built for the build stage's own architecture (no hard-coded `linux-x64`, so arm64 reviewers work). An `IDesignTimeDbContextFactory` in the data project reads `ConnectionStrings__Meridian` from the environment, so the bundle runs with no arguments and never boots the API host. It runs with `DOTNET_ENVIRONMENT=Production`, so it can't pick up Development user secrets.
4. **Member states are seeded through EF's seeding hook**: one `MemberStateSeeder` behind both `UseSeeding` (bundles call only the synchronous delegate) and `UseAsyncSeeding` (Testcontainers tests call `MigrateAsync`). It runs inside EF's migration lock after every migrate. If `member_state` is empty it parses `Seed:MemberStatesPath` (`/data_candidate/member_states.csv` in compose; the committed synthetic CSV in tests) with the import parser rules. If the file is missing it throws a message naming the file and folder. Then `migrate` exits non-zero and `api` and `worker` never start. Users stay in a migration (16).
5. **Mount**: only `migrate` mounts `${DATA_DIR:-./data_candidate}:/data_candidate:ro`. Short syntax: a missing folder is created empty, so the failure comes from our message, not a Docker error. Nothing else reads `data_candidate/`. The exports are uploaded through the UI. A root `.dockerignore` excludes `data_candidate/`, `.env`, `.git`, `.scratch`, `bin`, `obj` and `node_modules` from every build context, so assignment data can never be baked into an image.
6. **Angular is built in `web/Dockerfile`** (`node:24-alpine`: `npm ci`, generate the orval client from the committed `contracts/openapi.json`, `ng build`) and **served by `nginx:1.30-alpine`**. The web build needs no .NET. The API build rewrites `contracts/openapi.json`, and CI fails if the committed copy differs. Rejected: the API serving the files; `ng serve` in a container.
7. **Same origin, no CORS.** nginx serves the SPA (`try_files … /index.html`) and proxies `/api/` to `api:8080` with the prefix kept. Every API route lives under `/api`. The Angular app uses relative URLs, and `ng serve` proxies `/api` to `localhost:5080`. The API registers no CORS policy, because the user-switcher header would make every call a preflighted request. nginx sets `client_max_body_size 200m` (the upload limit from 10), `proxy_request_buffering off` (the API streams to Blob while hashing) and a 300 s read timeout. It forwards `X-Forwarded-For` and `X-Forwarded-Proto`, which the API honours from the compose network.
8. **Startup order and health checks**:
   - `db`: `pg_isready -h 127.0.0.1` over TCP, because the image's init server listens on the socket only, so the socket check can pass before init finishes.
   - `azurite`: `nc -z 127.0.0.1 10000`.
   - `migrate` waits for `db` to be healthy.
   - `api` and `worker` wait for `migrate` to complete successfully and for `azurite` to be healthy. Each creates the Blob container if it's missing (10).
   - `api`: `curl -fsS /health/ready` (database and Blob reachable); `/health/live` stays process-only. `curl` is the one package added to the runtime image, because `aspnet:10.0` ships without curl or wget.
   - `web` waits for `api` to be healthy and checks itself with `wget --spider`.
   - The worker has no Docker health check: its liveness is the job heartbeat (10). It gets `stop_grace_period: 30s`, so a stop finishes the vote it is committing.
   - `restart: unless-stopped` on everything except `migrate` (`"no"`). Health checks use `start_interval: 1s`, so a clean start isn't held back by 10 s intervals.
9. **Ports**: every host port binds to `127.0.0.1` only (the user switcher is not real authentication), and each is changeable in `.env`: web `8080`, api `5080` (OpenAPI document and `ng serve`), db `5433` (5432 is often taken), Azurite `10000`. Inside the network: `api:8080`, `db:5432`, `azurite:10000`.
10. **Configuration**: defaults that are safe to publish live in `appsettings.json`. Compose passes only what differs in containers, as environment variables. Keys:
    - `ConnectionStrings:Meridian`
    - `Blob:ConnectionString`, `Blob:Container` (`imports`)
    - `Imports:MaxConcurrent` (1), `Imports:MaxUploadBytes` (200 MB)
    - `Jobs:PollInterval` (1 s), `Jobs:HeartbeatInterval` (10 s), `Jobs:StaleAfter` (60 s), `Jobs:MaxAttempts` (3), `Jobs:ImportTimeout` (30 min), `Jobs:SnapshotTimeout` (10 min)
    - `Seed:MemberStatesPath`
11. **Secrets**:
    - Compose works with **no `.env`**. Interpolation defaults cover the two local-only values: the throwaway PostgreSQL password `meridian_local` and the Azurite connection string. Azurite's account key is Microsoft's published emulator key, not a secret.
    - Real values go only in the gitignored `.env`: `BLOB_CONNECTION_STRING` for real Azure Blob, later the PDF bonus keys. A committed `.env.example` lists every variable with a comment.
    - `dotnet run` outside Docker uses `appsettings.Development.json` (pointing at compose's `db` and `azurite` on localhost) plus **user-secrets** for real Azure values. Containers never read user-secrets.
12. **Volumes**: named `pgdata` at `/var/lib/postgresql` (the PostgreSQL 18 path, not `/data`) and `azurite-data` at `/data`. Uploaded files survive restarts, like the database. `docker compose down -v` is the documented reset.
13. **The sandbox proxy stays off the reviewer's path.** The committed Dockerfiles and `compose.yaml` contain no proxy or CA lines.
    - `.claude/sandbox/prepare-images.sh` builds local images that shadow `dotnet/sdk:10.0` and `node:24-alpine` with the sandbox CA trusted.
    - `.claude/sandbox/compose.sandbox.yaml` adds `build.network: host` and the proxy build args.
    - It is passed explicitly with `-f`. It is never named `compose.override.yaml`, because Compose loads that file automatically.
    - Fallback if shadowing doesn't take: sandbox-only copies of the two Dockerfiles. Whether `.claude/sandbox/` ships is ticket 23's call.
14. **CI** (`.github/workflows/ci.yml`, on push and PR, three parallel jobs):
    - `backend`: build with warnings as errors, an OpenAPI contract drift check, `dotnet ef migrations has-pending-model-changes`, and `dotnet test` with Testcontainers.
    - `web`: `npm ci`, generate the client, lint and test (per 20), build.
    - `compose`: `docker compose config`, then `DATA_DIR=./tests/data docker compose up -d --build` with the committed synthetic member states. It polls the web page and one authenticated `/api` call through nginx, asserts `migrate` exited 0, prints logs on failure, and always runs `down -v`.
    - No image is pushed: there is no deployment target.
15. **Clean-clone experience and README**:
    - Prerequisite: Docker with Compose v2.24 or later.
    - Steps: (1) put the assignment files in `data_candidate/` (`member_states.csv` is required to start; the exports are uploaded through the UI); (2) `docker compose up --build`; (3) open `http://localhost:8080` and pick a user in the header.
    - The README also says: the first build takes a few minutes; ports and values are changed by copying `.env.example` to `.env`; to use real Azure Blob, set `BLOB_CONNECTION_STRING`; to reset, `docker compose down -v`; if `migrate` fails, read `docker compose logs migrate`; for development, run `docker compose up db azurite`, then `dotnet run` and `ng serve`.
    - It never mentions the sandbox file.
