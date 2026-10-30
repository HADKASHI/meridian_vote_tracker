# Running on Azure

The app runs against Azure Blob Storage and Azure Database for PostgreSQL by an override file (ADR 0009). Plain `docker compose up` is unchanged: it still starts its own database and Azurite and needs no account and no `.env`.

## What you need

- An Azure Database for PostgreSQL flexible server on **PostgreSQL 17 or 18** (see [supported versions](postgres-versions.md)), with a database **dedicated to this app** that is empty or already migrated by it. `migrate` applies the migrations to it as it does locally, and seeds the member states.
- A storage account. The app creates its container (`imports`) on first start.
- The server's firewall must allow the machine running compose.
- `candidate_data/member_states.csv`, as for a local run: only `migrate` reads it.

## Run it

Copy `.env.example` to `.env` and set the two connection strings. `.env` is ignored by git; never put either value in a committed file.

```bash
AZURE_POSTGRES_CONNECTION_STRING=Host=<server>.postgres.database.azure.com;Database=meridian;Username=<user>;Password=<password>;SSL Mode=Require
BLOB_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net
```

```bash
docker compose -f compose.yaml -f compose.azure.yaml up --build
```

The override starts `migrate`, `api`, `worker` and `web`, and neither `db` nor `azurite`. If either variable is missing, compose stops and says which.

## TLS and a failing connection

Azure Database for PostgreSQL refuses a connection without TLS, so the override sets `Database__RequireTls`: a connection string whose `SSL Mode` is not `Require`, `VerifyCA` or `VerifyFull` is reported before the server refuses it.

Every service checks both connections when it starts and logs one line per failure, naming the service and the host, for example:

```
Startup check failed for database: Could not connect to the database at <server>.postgres.database.azure.com:5432: ...
Startup check failed for blob: Could not reach the Blob storage account '<account>': ...
```

`migrate` runs the check first and stops with exit code 1 when a connection fails, so a wrong string never reaches the migration bundle. The lines hold no password and no key. `/health/ready` also reports the database and Blob.

## Document Intelligence and Application Insights

- **Document Intelligence** reads the voting sheets: set `DOCINTEL_ENDPOINT` and `DOCINTEL_KEY` in `.env`. Without them a sheet fails once, saying no reader is configured.
- **Application Insights is configuration only.** Set `APPLICATIONINSIGHTS_CONNECTION_STRING` in `.env` and traces and logs go to it; leave it empty and nothing is sent. Nothing else needs to change.

## Downloads

The original of any import (a CSV or a voting sheet PDF) and of a sheet waiting in Review is downloaded through the API (`GET /api/imports/{id}/file`, `GET /api/extractions/{id}/file`), for analysts and the admin. There is no link to the blob that works without those checks. Logs hold ids and size, never names or content.

## Check it live

Everything above is proven against Azurite and a local PostgreSQL. The live Azure services are checked by you, on your own machine, with your own `.env`; run `scripts/check-azure.sh` (below). No credential is sent to this repository or to a cloud session.
