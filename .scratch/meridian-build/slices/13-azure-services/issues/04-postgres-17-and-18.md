# 04: PostgreSQL 17 and 18 proven by CI

**What to build:** the PostgreSQL image becomes a variable in compose and in the integration test fixture, 18 stays the default, and a CI job runs the whole integration suite on `postgres:17` as well as on 18, so a managed server on either version is known to work (ADR 0009). Nothing is changed to suit one version; if the suite fails on 17, the cause is found and fixed in the code.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] `compose.yaml` takes the image from a variable (for example `POSTGRES_IMAGE`) defaulting to `postgres:18`; plain `docker compose up` on a clean clone behaves exactly as before
- [x] The test fixture takes the same variable and defaults to `postgres:18`
- [x] CI runs the domain and integration tests on 18 and the integration suite on `postgres:17`, both required
- [x] The whole integration suite passes on `postgres:17`
- [x] The supported versions (17 and 18) are recorded in the ADR's neighbouring docs (a short note in `docs/`), not in the README
- [x] No migration is added
