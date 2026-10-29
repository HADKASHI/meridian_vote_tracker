# EF Core only, with rules in C# and facts from SQL

Status: accepted

## Context

The brief asks for EF Core and explains data access as a decision (section 9). The schema relies on triggers, views, partial unique indexes and row locks, and the riskiest rules (classification, scoring) must be easy to test and to explain. The historic import must finish within 5 minutes.

## Decision

- **EF Core is the one data-access library**, with the Npgsql provider and snake_case names. Code-first migrations hold the whole schema; what EF can't model (the guard trigger, the views, deferred keys) is SQL inside the same migrations.
- A one-shot **`migrate` service** runs the EF migration bundle and seeds the member states before the API and worker start, so app processes never race to migrate.
- **Use cases, not tables**: each write that must not go wrong (writing an import's vote, deciding a conflict, setting a position, calculating a snapshot) lives in one module that owns its transaction and lock. Reads are no-tracking projections straight into response DTOs.
- **Rules in C#, facts from SQL**: classification and scoring are pure functions with no database access, unit-tested from the numbered rules. A snapshot reads facts from the current views and scores them in C#.
- The per-vote lock is `SELECT … FOR UPDATE` on the vote row, taken first by an import's vote transaction and by a decision.
- **The bulk path sits behind one seam**, `ImportWriter` (one vote per transaction). It reads through EF, but writes a vote's verdicts with PostgreSQL binary COPY, because EF's `SaveChanges` missed the 5-minute budget (see Measured). Classification is untouched.
- Each area of the API is its own **feature module** with its own registration, so slices are built in parallel without touching each other.

## Considered options

- **Dapper or raw ADO beside EF**: two data-access styles for one small team.
- **A generic repository**: duplicates EF and splits the work by table rather than by use case.
- **`MigrateAsync()` at startup**: races once there is more than one process.
- **One set-based SQL statement for scoring**: faster, but it would hide the scoring rules from tests and from this record.
- **EF's in-memory provider for tests**: runs no triggers, locks or constraints, so tests use real PostgreSQL.

## Consequences

- The job runner's queue statements are plain SQL on a pooled data source, because they must be short and conditional on the attempt.
- The COPY writer takes its table, column and type mappings from the EF model, so the schema is still defined once; a new column on those tables needs no change in the writer.

## Measured

The `Performance` test (`dotnet test --project tests/Meridian.Integration.Tests -- --filter-trait "Category=Performance"`, or the manual Performance workflow) imports a synthetic file shaped like the historic one: 971,327 rows, 6,480 interleaved votes, 98 MiB. It runs the real API and worker in one process with PostgreSQL and Azurite in Docker on the same machine, so the figures are for a shared-CPU cloud container, not a laptop.

| | EF `SaveChanges` | Binary COPY |
|---|---|---|
| Import time (budget 05:00) | 06:50 | **03:02** |
| Peak working set, whole test process | 2,223 MiB | 1,488 MiB |
| Growth during the import (budget about 1 GB) | 1,471 MiB | **732 MiB** |

With EF, `SaveChanges` took 336 of 410 seconds; parsing (8 s), locking (9 s), reading approved values (19 s), classifying (10 s) and committing (17 s) were small. After the change the growth is under 1 GB, so the parser stays single-pass; if a later file pushes it over, the parser's two-pass form is the next step. The test process also holds the 98 MiB file, the API's copy of the upload and the host, so the growth figure, not the peak, is the one to compare with the budget.
