# Slice 13: Azure services and file download

Status: done
Blocked by: Slice 9
Runs in parallel with: nothing (slice 8 waits for it)
PR title: `Slice 13: Azure services and file download`
Requirements: none (an extra)
User stories in the spec: 124–127

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions", then [ADR 0008](../../../../docs/adr/0008-pdf-extraction-through-review.md) and [ADR 0009](../../../../docs/adr/0009-azure-run-postgres-versions-and-downloads.md).

## Goal

An analyst can download the original file of any import (CSV or voting sheet PDF) and of a sheet waiting in Review, and an operator can run the app against real Azure Blob Storage and Azure Database for PostgreSQL, with PostgreSQL 17 proven alongside 18 and a guided check of the live Azure services.

## Lane

The download endpoints (`GET /api/imports/{importId}/file` in the imports module, `GET /api/extractions/{extractionId}/file` in the extractions module), one shared download helper next to the upload helper in `Meridian.Platform`, and Download buttons in the imports and review (Extractions) feature folders. Plus: `compose.azure.yaml`, the Postgres image variable in compose and in the test fixture, a CI job running the integration suite on `postgres:17`, a TLS-aware connection check with a clear startup message, a `docs/azure.md` the finishing pass links from the README (the README itself belongs to slice 8), and the guided live-check script for the operator's own machine.

Two new endpoints are a contract change outside the slice's endpoints: flag it at the top of the PR and regenerate the OpenAPI file, never edit it. Expect no migration: confirm that the import and the extraction already store the file name and content type the download needs; if the extraction does not, derive the name from the sheet's vote number and say so rather than adding a migration.

## Data it arranges

A CSV import and an approved voting sheet import arranged with slice 0's fixture builders and Azurite, and a sheet waiting in Review. Real sheets and credentials are never committed or placed in this environment.

## Done when

- the Imports list and the import page offer Download for CSV and PDF imports, and the Extractions tab offers it for a sheet waiting in review; the file streams from Blob with its original name and content type as an attachment, for analysts and the admin only (401 and 403 proven by the role matrix), and the download is logged with ids and size but no content
- `docker compose -f compose.yaml -f compose.azure.yaml up` uses the Azure Blob and PostgreSQL connection strings from `.env` with TLS to the database, and a failing connection is named in the startup message; plain `docker compose up` on a clean clone behaves exactly as before
- the whole integration suite passes on `postgres:17` in CI as well as on 18, and the supported versions are recorded
- Application Insights is documented as configuration only
- the guided script checks Blob upload and download, the database on its real version, a Document Intelligence call on a real sheet and Application Insights, using the operator's own `.env`, and prints what passed and what did not; no secret is written anywhere
- no migration is added unless the slice stops and says why in its PR

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.

## Scope change after merge

The guided check covers Blob and Document Intelligence only. The database and Application Insights were dropped from it, and an Entra sign-in built for PostgreSQL was removed again (dev-ops problems reaching the managed server from the cloud session; see ADR 0009's update and DECISIONS.md). Entra ID sign-in stays for Blob and Document Intelligence.
