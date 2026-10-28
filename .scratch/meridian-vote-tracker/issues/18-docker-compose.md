# Design Docker Compose and image builds

Type: grilling
Status: claimed
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
