# Running on Azure

The app runs against Azure Blob Storage and Azure Database for PostgreSQL by an override file (ADR 0009). Plain `docker compose up` is unchanged: it still starts its own database and Azurite and needs no account and no `.env`.

## What you need

- An Azure Database for PostgreSQL flexible server on **PostgreSQL 17 or 18** (see [supported versions](postgres-versions.md)), with a database **dedicated to this app** that is empty or already migrated by it. `migrate` applies the migrations to it as it does locally, and seeds the member states.
- A storage account. The app creates its container (`imports`) on first start.
- A way to sign in: either **keys and a password**, or **Microsoft Entra ID** (see [Signing in with Microsoft Entra ID](#signing-in-with-microsoft-entra-id)). Use Entra ID when your account cannot read the storage keys or create a database password.
- The server's firewall must allow the machine running compose.
- `candidate_data/member_states.csv`, as for a local run: only `migrate` reads it.

## Run it

Copy `.env.example` to `.env` and set the connection settings. `.env` is ignored by git; never put a key or password in a committed file.

```bash
# With a password and a key
AZURE_POSTGRES_CONNECTION_STRING=Host=<server>.postgres.database.azure.com;Database=meridian;Username=<user>;Password=<password>;SSL Mode=Require
BLOB_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net

# Or with Microsoft Entra ID: no password, no key
AZURE_POSTGRES_CONNECTION_STRING=Host=<server>.postgres.database.azure.com;Database=meridian;Username=<your Entra user>;SSL Mode=Require
BLOB_SERVICE_URI=https://<account>.blob.core.windows.net/
```

```bash
docker compose -f compose.yaml -f compose.azure.yaml up --build
```

The override starts `migrate`, `api`, `worker` and `web`, and neither `db` nor `azurite`. If `AZURE_POSTGRES_CONNECTION_STRING` is missing, compose stops and says so; if neither `BLOB_SERVICE_URI` nor `BLOB_CONNECTION_STRING` is set, each service says at start that no Blob storage is configured.

## TLS and a failing connection

Azure Database for PostgreSQL refuses a connection without TLS, so the override sets `Database__RequireTls`: a connection string whose `SSL Mode` is not `Require`, `VerifyCA` or `VerifyFull` is reported before the server refuses it.

Every service checks both connections when it starts and logs one line per failure, naming the service and the host, for example:

```
Startup check failed for database: Could not connect to the database at <server>.postgres.database.azure.com:5432: ...
Startup check failed for blob: Could not reach the Blob storage account '<account>': ...
```

`migrate` runs the check first and stops with exit code 1 when a connection fails, so a wrong string never reaches the migration bundle. The lines hold no password and no key. `/health/ready` also reports the database and Blob.

## Document Intelligence and Application Insights

- **Document Intelligence** reads the voting sheets: set `DOCINTEL_ENDPOINT` and `DOCINTEL_KEY` in `.env`. With the endpoint and **no key** the reader signs in with Microsoft Entra ID instead (below). Without an endpoint a sheet fails once, saying no reader is configured.
- **Application Insights is configuration only.** Set `APPLICATIONINSIGHTS_CONNECTION_STRING` in `.env` and traces and logs go to it; leave it empty and nothing is sent. Nothing else needs to change.

## Signing in with Microsoft Entra ID

Three settings switch a service from a password or key to an Entra ID sign-in. Anything else works exactly as before, and Azurite and the local database never touch Entra.

| Service | Entra ID is used when | What it uses |
|---|---|---|
| PostgreSQL | the host ends in `.postgres.database.azure.com` and the connection string has **no `Password`** | an access token for `https://ossrdbms-aad.database.windows.net/.default` as the password, renewed before it expires. `Username` is the Entra principal (for example `you@contoso.com`), and `SSL Mode=Require` still applies |
| Blob | `Blob__ServiceUri` (`BLOB_SERVICE_URI`) is set; it wins over `Blob__ConnectionString` | `new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential())` |
| Document Intelligence | `DocumentIntelligence__Endpoint` is set and `DocumentIntelligence__Key` is empty | a bearer token for `https://cognitiveservices.azure.com/.default`. The resource needs its custom subdomain endpoint |

The sign-in is `DefaultAzureCredential`: `az login` on your machine, a service principal from `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET`, or a managed identity when the app runs in Azure.

The identity also needs the right to use each service. None of this is checked from the repository:

- **Blob:** the *Storage Blob Data Contributor* role on the storage account (or container).
- **PostgreSQL:** Microsoft Entra authentication switched on for the server, and a database role for your principal (created by the server's Entra administrator with `pgaadauth_create_principal`) that owns, or can create objects in, the dedicated database, since `migrate` creates the schema.
- **Document Intelligence:** the *Cognitive Services User* role on the resource.

A container has no `az login`. With compose either give the services a service principal (set the three `AZURE_*` variables in `.env`), or, if you can only sign in with `az login`, run the services on your machine with the same settings:

```bash
az login
export ConnectionStrings__Meridian='Host=<server>.postgres.database.azure.com;Database=meridian;Username=<your Entra user>;SSL Mode=Require'
export Blob__ServiceUri='https://<account>.blob.core.windows.net/'
export Seed__MemberStatesPath="$PWD/candidate_data/member_states.csv"   # absolute: dotnet ef runs from src/Meridian.Data
dotnet tool restore
dotnet ef database update --project src/Meridian.Data     # migrations and member states
dotnet run --project src/Meridian.Api                      # in one terminal
dotnet run --project src/Meridian.Worker                   # in another
```

When a sign-in cannot be had, the startup line names the service and says why it matters, without quoting the credential's own message:

```
Startup check failed for database: Could not connect to the database at <server>...: ... It signs in with Microsoft Entra ID; run 'az login' (or give the process a service principal or managed identity) and check the account has access.
```

## Downloads

The original of any import (a CSV or a voting sheet PDF) and of a sheet waiting in Review is downloaded through the API (`GET /api/imports/{id}/file`, `GET /api/extractions/{id}/file`), for analysts and the admin. There is no link to the blob that works without those checks. Logs hold ids and size, never names or content.

## Check it live

Everything above is proven against Azurite and a local PostgreSQL. Blob and Document Intelligence are checked live by you, on your own machine, with your own `.env`; run the guided check below. No credential is sent to this repository or to a cloud session.

## Guided live check

Run this on your own machine, with the `.env` you filled in above and a **real voting sheet PDF** from your own files. It needs the .NET SDK (`global.json` names the version). If you use Entra ID, run `az login` first: the check signs in the same way the app does.

```bash
scripts/check-azure.sh /path/to/a/real/sheet.pdf
```

Without the path it asks for one, and Enter skips that check. The sheet is read where it lies: it is never copied, and real sheets are never committed. The script reads `.env` itself, keeps every value in memory, writes no file, and prints no secret and none of the sheet's content.

| Check | What passing means |
|---|---|
| Blob | a 64-byte test blob was uploaded to the app's container and downloaded with the same bytes, then deleted. With `BLOB_SERVICE_URI` it also proves the Entra sign-in and the *Storage Blob Data Contributor* role |
| Document Intelligence | the app's own adapter (with the key, or with Entra ID if `DOCINTEL_KEY` is empty) read the sheet through the `prebuilt-layout` model; the report gives the pages, how many grid rows the mapper found, and whether the vote id and printed totals were found. Counts only |

Each line starts `PASS`, `FAIL` or `SKIP` (a setting or the sheet was not given), with the reason for anything that did not pass. The exit code is 0 only when both passed.

Running it with a real sheet closes the limit ADR 0008 admits: the Document Intelligence adapter has so far only met a hand-written fixture. If the report says the mapper found no grid rows, or no totals, the layout model returned that sheet in a shape the mapper does not know yet; keep the sheet and the report's counts and take them to the next build.

### What the check does not cover

The database and Application Insights are **not** checked live. We ran into dev-ops problems: the Azure Database for PostgreSQL server could not be reached from the cloud session the check was run in (its name does not resolve there), and its Entra sign-in could not be set up and proven in the time available. Application Insights accepted a test trace, but nobody confirmed it arrived. So this task runs on the local PostgreSQL and does not rely on Application Insights, even though ideally it would use both.

The code for both is in place and tested against stand-ins: a password-less connection string to an Azure server signs in with Entra ID, and Application Insights switches on with `APPLICATIONINSIGHTS_CONNECTION_STRING`. A first run against the real server is still to be done; if it fails, the startup message names the connection.
