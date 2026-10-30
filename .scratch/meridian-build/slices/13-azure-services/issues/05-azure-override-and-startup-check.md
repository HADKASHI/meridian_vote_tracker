# 05: Run against Azure with an override file

**What to build:** an operator runs `docker compose -f compose.yaml -f compose.azure.yaml up` and the app uses the Azure Blob and Azure Database for PostgreSQL connection strings from `.env`, with TLS to the database. A connection that cannot be made is named in the startup message (Blob or database, and why) instead of a stack trace. The `migrate` container applies migrations to the Azure database as it does locally.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] `compose.azure.yaml` replaces the local `db` and Azurite with the connection strings from `.env`, requires TLS to the database and starts no local database or emulator
- [ ] A TLS-aware connection check at startup of `migrate`, `api` and `worker` names the failing connection (database or Blob) and the reason in a clear message, with no password or key in it
- [ ] `docker compose config -q` passes for the plain file and for the two files together; plain `docker compose up` on a clean clone with no `.env` behaves exactly as before and the compose smoke test stays green
- [ ] The check is tested at its seam (a connection string that fails names the right service and holds no secret)
- [ ] `.env.example` or the docs list the new variables without any value that is a secret
- [ ] No migration is added
