# Supported PostgreSQL versions

The app runs on **PostgreSQL 17 and 18** (ADR 0009). 18 is the default; the Azure Database for PostgreSQL flexible server in use runs 17.11.

| Version | Where it is proven |
|---|---|
| 18 | the whole domain and integration suite in CI, and the compose stack |
| 17 | the whole integration suite in CI (job `integration-postgres-17`) |

The image is a variable in both places: `POSTGRES_IMAGE` in `compose.yaml` (default `postgres:18`) and the same variable in the test fixture. To try the other version locally:

```bash
POSTGRES_IMAGE=postgres:17 dotnet test --project tests/Meridian.Integration.Tests -- --filter-not-trait "Category=RealData" --filter-not-trait "Category=Performance"
```

Nothing in the schema or the code is written for one version; a failure on either is fixed in the code, not by dropping a version.
