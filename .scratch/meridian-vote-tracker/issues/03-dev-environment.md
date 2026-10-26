# Set up the dev environment

Type: task
Status: resolved

## Question

Make this cloud container able to build and run the stack: install the .NET SDK (dot.net and builds.dotnet.microsoft.com are blocked by the proxy; packages.microsoft.com and NuGet are reachable), start the Docker daemon, and confirm a PostgreSQL container and Angular CLI work. Record the exact steps so later sessions can repeat them, possibly as a SessionStart hook.

## Answer

Repeatable with [`.claude/cloud-dev-setup.sh`](../../../.claude/cloud-dev-setup.sh) (idempotent; run at the start of each cloud session). Verified in this session:

- **.NET SDK 10.0.113** from Ubuntu 24.04's own archive (`apt-get install dotnet-sdk-10.0`). Microsoft's feed is reachable but carries no SDK for 24.04, and dot.net is blocked. This is feature band 10.0.1xx while the newest SDK is 10.0.4xx, so any `global.json` must use `rollForward: latestFeature` from `10.0.100`, not pin 10.0.4xx. A `webapi` project with the Npgsql EF Core provider restores from NuGet and builds.
- **Node 24.22.1** in `/opt/node24`. The preinstalled Node 22.22.0 fails Angular 22's engine check (`^22.22.3 || ^24.15.0`). Changing the shell profile was not permitted, so commands must start with `export PATH=/opt/node24/bin:$PATH`. Angular CLI 22.2.4 runs.
- **Docker 29.9.2**: `dockerd` started by hand. `postgres:18` (18.6) runs and accepts connections; `mcr.microsoft.com/dotnet/sdk:10.0` and `node:24-alpine` pull. Testcontainers-style integration tests are possible.
- **Container builds in this sandbox** reach npm and NuGet only with `--network host`, the proxy variables passed in, and `/root/.ccr/ca-bundle.crt` trusted inside the container. A reviewer's machine needs none of this, so the committed Dockerfiles stay clean and any proxy handling lives in a sandbox-only override that is not part of the submission.
