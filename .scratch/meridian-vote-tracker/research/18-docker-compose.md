# Docker Compose and image builds (supporting note)

Decided in [Design Docker Compose and image builds](../issues/18-docker-compose.md). This note holds the facts checked, the sketches and the rejected alternatives. Folder names (`src/`, `web/`, `contracts/`) are working names; the skeleton may rename them without changing any decision.

## Facts checked (2026-10-28, primary sources)

| Fact | Source |
|---|---|
| `mcr.microsoft.com/dotnet/sdk:10.0` (= 10.0.403) and `aspnet:10.0` (= 10.0.14) exist, plus `-noble`, `-noble-chiseled`, `-alpine` variants | `mcr.microsoft.com/v2/dotnet/{sdk,aspnet}/tags/list` |
| `aspnet:10.0` is Ubuntu noble with ICU; it sets `ASPNETCORE_HTTP_PORTS=8080`, creates non-root user `app` (`APP_UID=1654`), and ships **no curl or wget** | `dotnet/dotnet-docker` `src/runtime-deps/10.0/noble/amd64/Dockerfile`, `src/aspnet/10.0/noble/amd64/Dockerfile` |
| Azurite latest is `3.37.0`; image is `node:22-alpine` (busybox `nc` and `wget` available), `VOLUME /data`, ports 10000-10002 | `mcr.microsoft.com/v2/azure-storage/azurite/tags/list`; `Azure/Azurite` `Dockerfile` |
| `postgres:18` = 18.6 (Debian trixie); `PGDATA=/var/lib/postgresql/18/docker`, volume at `/var/lib/postgresql`; `19rc1` exists, not GA | Docker Hub `library/postgres` tags; [research/02](02-versions-and-tooling.md) |
| nginx stable line is 1.30 (`1.30.6`), mainline 1.31; `nginx:1.30-alpine` exists | Docker Hub `library/nginx` tags |
| `node:24-alpine` exists (24.22.1) | Docker Hub `library/node` tags |
| A migration bundle is "equivalent to `dotnet ef database update`", needs no SDK or source, uses EF's migration lock, and **runs `UseSeeding` (the synchronous delegate)**; `MigrateAsync` runs `UseAsyncSeeding`. Seeding runs even when no migration was applied, so it must check before inserting | `dotnet/EntityFramework.Docs` `core/modeling/data-seeding.md` (deployment behaviour table), `core/managing-schemas/migrations/applying.md` (Bundles) |
| Run a bundle with the environment set explicitly, or it can load **Development user secrets** | same `applying.md` ("This also prevents a bundle from loading development user secrets unexpectedly") |
| Compose `depends_on` conditions: `service_started`, `service_healthy`, `service_completed_successfully`; local Compose is v5.6.1 | Compose spec; `docker compose version` here |
| The postgres image's init runs a temporary server with `listen_addresses=''` (socket only), so `pg_isready` over the socket can report ready before init scripts finish; over TCP (`-h 127.0.0.1`) it cannot | `docker-library/postgres` `docker-entrypoint.sh` (`docker_temp_server_start`) |

## Compose sketch (illustrative, not the file)

```yaml
name: meridian

x-backend: &backend
  build: { context: ., dockerfile: src/Dockerfile }
  image: meridian-backend:local
  environment: &backend-env
    DOTNET_ENVIRONMENT: Production
    ConnectionStrings__Meridian: Host=db;Database=meridian;Username=meridian;Password=${POSTGRES_PASSWORD:-meridian_local}
    Blob__ConnectionString: ${BLOB_CONNECTION_STRING:-DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=<Azurite well-known key>;BlobEndpoint=http://azurite:10000/devstoreaccount1;}
    Imports__MaxConcurrent: ${IMPORTS_MAX_CONCURRENT:-1}

services:
  db:
    image: postgres:18
    environment:
      POSTGRES_DB: meridian
      POSTGRES_USER: meridian
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-meridian_local}
    volumes: [pgdata:/var/lib/postgresql]
    ports: ["127.0.0.1:${DB_PORT:-5433}:5432"]
    healthcheck:
      test: ["CMD", "pg_isready", "-h", "127.0.0.1", "-U", "meridian", "-d", "meridian"]
      interval: 10s
      start_period: 30s
      start_interval: 1s
    restart: unless-stopped

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:3.37.0
    command: ["azurite-blob", "--blobHost", "0.0.0.0", "--location", "/data", "--skipApiVersionCheck"]
    volumes: [azurite-data:/data]
    ports: ["127.0.0.1:${AZURITE_PORT:-10000}:10000"]
    healthcheck:
      test: ["CMD", "nc", "-z", "127.0.0.1", "10000"]
      interval: 10s
      start_interval: 1s
    restart: unless-stopped

  migrate:
    <<: *backend
    entrypoint: ["/app/migrate/efbundle"]
    environment:
      <<: *backend-env
      Seed__MemberStatesPath: /data_candidate/member_states.csv
    volumes: ["${DATA_DIR:-./data_candidate}:/data_candidate:ro"]
    depends_on:
      db: { condition: service_healthy }
    restart: "no"

  api:
    <<: *backend
    entrypoint: ["dotnet", "/app/api/Meridian.Api.dll"]
    ports: ["127.0.0.1:${API_PORT:-5080}:8080"]
    depends_on: &after-migrate
      migrate: { condition: service_completed_successfully }
      azurite: { condition: service_healthy }
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://127.0.0.1:8080/health/ready"]
      interval: 10s
      start_period: 20s
      start_interval: 1s
    restart: unless-stopped

  worker:
    <<: *backend
    entrypoint: ["dotnet", "/app/worker/Meridian.Worker.dll"]
    depends_on: *after-migrate
    stop_grace_period: 30s
    restart: unless-stopped

  web:
    build: { context: ., dockerfile: web/Dockerfile }
    ports: ["127.0.0.1:${WEB_PORT:-8080}:80"]
    depends_on:
      api: { condition: service_healthy }
    healthcheck:
      test: ["CMD", "wget", "-q", "--spider", "http://127.0.0.1/"]
      interval: 10s
    restart: unless-stopped

volumes:
  pgdata:
  azurite-data:
```

`api` and `worker` reach `db` only after `migrate`, which itself waited for `db` to be healthy, so they need no separate `db` condition.

## Backend Dockerfile sketch (`src/Dockerfile`, context = repo root)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY .config/dotnet-tools.json .config/
COPY src/*/*.csproj  ...            # project files first, for a cached restore layer
RUN dotnet tool restore && dotnet restore src/Meridian.sln
COPY src/ src/
RUN dotnet publish src/Meridian.Api    -c Release --no-restore -o /out/api \
 && dotnet publish src/Meridian.Worker -c Release --no-restore -o /out/worker \
 && dotnet ef migrations bundle -c Release \
      --project src/Meridian.Data --startup-project src/Meridian.Worker \
      -o /out/migrate/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*        # for the api health check only
COPY --from=build /out /app
USER $APP_UID
ENTRYPOINT ["dotnet", "/app/api/Meridian.Api.dll"]
```

No `--target-runtime` on the bundle: it is framework-dependent and built for the build stage's own architecture, so the same Dockerfile works on an Apple Silicon reviewer's machine (arm64) and on CI (amd64). Hard-coding `linux-x64` would break arm64.

## Web Dockerfile and nginx sketch (`web/Dockerfile`, context = repo root)

```dockerfile
FROM node:24-alpine AS build
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY contracts/openapi.json /contracts/openapi.json
COPY web/ ./
RUN npm run build          # generates the orval client from ../contracts/openapi.json, then ng build

FROM nginx:1.30-alpine
COPY web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/<app>/browser /usr/share/nginx/html
```

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;

  location /api/ {
    proxy_pass http://api:8080;            # no trailing slash: /api prefix kept
    proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    client_max_body_size 200m;             # the upload limit from ticket 10
    proxy_request_buffering off;           # stream the upload; the API hashes it on the way to Blob
    proxy_read_timeout 300s;
  }

  location = /index.html { add_header Cache-Control "no-cache"; }
  location / { try_files $uri $uri/ /index.html; }   # SPA deep links
  # hashed assets: Cache-Control "public, max-age=31536000, immutable"
}
```

## Sandbox override (not on the reviewer's path)

The cloud sandbox needs three things a reviewer's machine does not: build containers on the host network, proxy variables during the build, and the sandbox CA trusted by NuGet and npm inside the build stages. Committed Dockerfiles stay untouched:

1. `.claude/sandbox/prepare-images.sh` (run once per session, after `cloud-dev-setup.sh`) builds local images that **shadow** the two base images that download packages, `mcr.microsoft.com/dotnet/sdk:10.0` and `node:24-alpine`: `FROM` the real tag, copy `/root/.ccr/ca-bundle.crt` in, `update-ca-certificates`, and set `NODE_EXTRA_CA_CERTS`. BuildKit's docker driver uses a local image for `FROM` unless the build forces a pull, so the committed Dockerfiles pick up the trusted CA without a change.
2. `.claude/sandbox/compose.sandbox.yaml` adds `build.network: host` and the predefined proxy build args (`HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY`; Docker passes these without an `ARG` line) to `migrate`, `api`, `worker` and `web`.
3. Used as `docker compose -f compose.yaml -f .claude/sandbox/compose.sandbox.yaml up --build`.

To verify in the skeleton: that the shadowed tags are used. Fallback: the sandbox file overrides `build.dockerfile` with sandbox copies of the two Dockerfiles that add the CA. Either way nothing is named `compose.override.yaml`, because Compose loads that file automatically, so a reviewer would run the sandbox settings without knowing.

## CI workflow sketch (`.github/workflows/ci.yml`)

On every push and pull request; `concurrency` cancels superseded runs on the same ref.

| Job | Steps |
|---|---|
| `backend` | setup .NET 10; `dotnet restore`; `dotnet build -warnaserror`; **contract drift**: `git diff --exit-code contracts/openapi.json` (the build rewrites it); **model drift**: `dotnet ef migrations has-pending-model-changes`; `dotnet test` (unit, and integration against PostgreSQL and Azurite through Testcontainers; the hosted Ubuntu runner has Docker) |
| `web` | setup Node 24 with npm cache; `npm ci`; generate the client from the committed contract; lint and tests as [Set the test strategy](../issues/20-test-strategy.md) decides; `npm run build` |
| `compose` | `docker compose config -q`; `DATA_DIR=./tests/data docker compose up -d --build`; poll `http://127.0.0.1:8080/` and one authenticated `/api/...` call through nginx (60 s limit); assert `migrate` exited 0; on failure print `docker compose logs`; always `docker compose down -v` |

The three jobs run in parallel. Nothing is pushed to a registry: there is no deployment target. The timed ~1M-row import is not in this workflow; its home is decided by ticket 20.

## Rejected alternatives

| Question | Chosen | Rejected and why |
|---|---|---|
| .NET images | One image, three entry points (`api`, `worker`, `migrate`) | **Separate images or Dockerfile targets per service**: the worker could use the smaller `runtime` base (tens of MB saved), but it means three builds of the same code in CI and on the reviewer's machine and a chance of mismatched versions between `api` and `worker`. **One host with a role flag** (`--role worker`): ticket 10 decided two entry points, and a role flag merges two dependency graphs into one `Program.cs` |
| Base image | `aspnet:10.0` (noble) | **`-noble-chiseled`**: no shell and no curl, so no health check without extra code, and harder to debug in a review. **`-alpine`**: invariant globalization by default; the importer's case and date normalisation should not depend on that. Neither saving matters here |
| API health check tool | `curl` installed in the runtime stage | **`bash /dev/tcp` trick**: works with no package, but reviewers have to puzzle it out. **A `--healthcheck` mode in the app**: code for a container concern. **No health check**: `web` could not wait for a working API and `docker compose ps` would show nothing useful |
| Migrations | EF migration bundle, seeding via `UseSeeding` | **`MigrateAsync()` at startup** (ticket 09: races with two processes). **SQL script through `psql`**: runs no seeding delegate and needs `psql` in an image. **`dotnet ef database update` in an SDK container**: a 1 GB SDK image at runtime, plus source in the image |
| Member-state seed | `UseSeeding`/`UseAsyncSeeding` → one seeder | **A second step after the bundle** (`efbundle && dotnet worker.dll seed`): needs a shell command line in compose, and tests would seed by a different path. **`HasData`**: the data would be committed in a migration, and it is assignment data that must stay out of git |
| Serving Angular | nginx container | **The API serves the static files** (`UseStaticFiles`): Node in the .NET build, every UI change rebuilds the backend image, and API and static traffic share one process. **`ng serve` in a container**: a development server, not what ships |
| Routing and CORS | Same origin: nginx proxies `/api/`; dev uses the Angular proxy | **CORS with configured origins**: the identity header from the user switcher makes every call a preflighted request, and it adds a setting that can be wrong. **Absolute API URL baked into the build**: one bundle per environment |
| Local passwords | Compose defaults for throwaway local values; `.env` overrides | **`env_file: .env` required**: a clean clone fails until the reviewer copies a file. **Generated passwords**: a setup script before `docker compose up` |
| Sandbox proxy | Separate file under `.claude/sandbox/` plus shadowed base images | **`compose.override.yaml`**: Compose loads it automatically. **Proxy and CA lines in the real Dockerfiles behind build args**: harmless, but visible to the reviewer and not needed by them. **Uncommitted file**: lost when the container is reclaimed (the reason `cloud-dev-setup.sh` is committed) |
| Seed file mount | Short syntax `./data_candidate:/data_candidate:ro` | **Long syntax with `create_host_path: false`**: Compose stops with a generic bind-mount error. The short syntax creates an empty folder, and the seeder then fails with a message that says which file to put where |
| Host ports | `127.0.0.1` only, configurable | **All interfaces**: the user switcher is not real authentication, so the app must not be reachable from the LAN. **Standard 5432 for PostgreSQL**: often already taken on a .NET developer's machine |
