# Running on Azure, supporting PostgreSQL 17 and 18, and downloads through the API

Status: accepted

## Context

The brief names Azure Blob Storage, PostgreSQL and optionally Application Insights, and Document Intelligence for the voting sheets (ADR 0008). Blob and Application Insights are already switched by connection string, and Document Intelligence by endpoint and key. The database was built and tested on PostgreSQL 18, but the Azure Database for PostgreSQL flexible server in use runs 17.11. Compose points the app at its own `db` container, and nothing sets TLS, which the managed server requires. Stored files (CSV imports and voting sheet PDFs) cannot be downloaded at all. ADR 0008 admits the Document Intelligence adapter has only met a hand-written fixture.

## Decision

- **Both PostgreSQL 17 and 18 are supported.** The image is a variable in compose and in the test fixture, 18 stays the default, and CI runs the whole integration suite on `postgres:17` as well. Nothing is changed to match one version; the suite proves that nothing in the schema or the code needs 18.
- **Azure is reached through an override file.** `compose.azure.yaml` replaces the local `db` and Azurite with the Azure Postgres and Blob connection strings from `.env` and requires TLS to the database. Plain `docker compose up` is unchanged, so a clean clone still runs with no Azure account and no `.env`. The `migrate` container applies migrations to the Azure database as it does locally, so the database should be empty or already migrated and dedicated to this app. A connection that fails is named in the startup message.
- **Application Insights stays configuration only.** Setting its connection string switches it on; the slice only documents that.
- **A file is downloaded through the API.** `GET /api/imports/{importId}/file` and `GET /api/extractions/{extractionId}/file` stream the stored original from Blob as an attachment with its original name and content type, to analysts and the admin, and log ids and size but never content. One shared helper beside the upload helper serves both. There is no public or SAS link, so access stays behind the role checks and each download can be logged.
- **Live Azure is checked by the operator, not in CI or in the cloud session.** A guided script run on the operator's machine with their own `.env` proves Blob upload and download, the database on its real version, a Document Intelligence call on a real sheet and Application Insights. No secret is stored, logged or sent anywhere else, and real sheets stay out of the repository.

## Considered options

- **Move everything to PostgreSQL 17 to match Azure:** more churn in compose, tests, CI and docs for a version the code does not depend on, and it would drop the newer version the project was built on.
- **Only check 17 by hand:** a hand check drifts; a CI job keeps it true.
- **SAS links for downloads:** a link works for anyone who holds it until it expires and skips the role matrix and the log; streaming through the API costs the API bandwidth, which is acceptable at this size and can be revisited with the upload path (ADR 0003).
- **Giving the cloud session the credentials:** the secrets would leave the operator's machine, and the session's network is not meant to reach their Azure resources.

## Consequences

- Two read endpoints are added to the contract after the walking skeleton fixed it.
- CI gains a job and runs longer.
- The finishing pass links `docs/azure.md` from the README.
- Closes the fixture-only limit in ADR 0008 once the operator runs the guided check.
