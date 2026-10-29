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
- **The bulk path sits behind one seam**, `ImportWriter`, built with EF (one vote per transaction). Classification is untouched. Binary COPY inside the writer is the known faster form (see Measured); it is not adopted.
- **Uploads stay capped at 200 MB** (`Imports:MaxUploadBytes`, with the same figure in nginx's `client_max_body_size`). A larger file is refused with 413 before any work starts. The historic file (98 MiB) fits under it.
- Each area of the API is its own **feature module** with its own registration, so slices are built in parallel without touching each other.

## Considered options

- **Dapper or raw ADO beside EF**: two data-access styles for one small team.
- **A generic repository**: duplicates EF and splits the work by table rather than by use case.
- **`MigrateAsync()` at startup**: races once there is more than one process.
- **One set-based SQL statement for scoring**: faster, but it would hide the scoring rules from tests and from this record.
- **EF's in-memory provider for tests**: runs no triggers, locks or constraints, so tests use real PostgreSQL.
- **Binary COPY in `ImportWriter`**: measured at 03:02 for the historic-sized file against 06:50 with EF, with half the memory growth. Not adopted: the decision was to keep the EF writer and accept the miss. The commit "Write a vote's rows with binary COPY" in the slice 2 pull request (#12) holds it, with its tests, if the budget has to be met.

## Consequences

- The job runner's queue statements are plain SQL on a pooled data source, because they must be short and conditional on the attempt.
- **The historic-sized file misses the 5-minute budget** with the EF writer: 06:50 in the measured container, and 07:07 on the first run. The budget is not met, by decision.
- **A file at the 200 MB cap is slower and heavier still.** These figures are estimates scaled from the 98 MiB measurement, not runs. At the measured 2,370 rows a second, about 2 million rows take roughly 14 minutes, inside `Jobs:ImportTimeout` (30 minutes). The single-pass parser grew by about 15 times the file's size, so such a file needs about 3 GB of worker memory, past the 1 GB point where the spec switches the parser to two passes. COPY and the two-pass parser are the next steps if larger or faster imports are needed.

## Measured

The `Performance` test (`dotnet test --project tests/Meridian.Integration.Tests -- --filter-trait "Category=Performance"`, or the manual Performance workflow) imports a synthetic file shaped like the historic one: about 971,000 rows, 6,480 interleaved votes, 98 MiB. It runs the real API and worker in one process with PostgreSQL and Azurite in Docker on the same machine, so the figures are for a shared-CPU cloud container, not a laptop. It currently fails its 5-minute check, which is the recorded result.

| | EF `SaveChanges` (in use) | Binary COPY (not adopted) |
|---|---|---|
| Import time (budget 05:00) | **06:50** (a second run; the first took 07:07) | 03:02 (a later run: 02:58) |
| Peak working set, whole test process | 2,223 MiB | 1,488 MiB |
| Growth during the import (budget about 1 GB) | **1,471 MiB** (the first run: 1,420 MiB) | 732 MiB |

With EF, `SaveChanges` took 336 of 410 seconds; parsing (8 s), locking (9 s), reading approved values (19 s), classifying (10 s) and committing (17 s) were small. The growth is over the spec's 1 GB line with EF, so the parser's two-pass form is also pending. The test process also holds the 98 MiB file, the API's copy of the upload and the host, so the growth figure, not the peak, is the one to compare with the budget.
