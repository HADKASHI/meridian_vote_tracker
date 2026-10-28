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
- **The bulk path sits behind one seam**, `ImportWriter`, built with EF (one vote per transaction). Only if the 5-minute budget is missed do its internals switch to PostgreSQL binary COPY; classification is untouched.
- Each area of the API is its own **feature module** with its own registration, so slices are built in parallel without touching each other.

## Considered options

- **Dapper or raw ADO beside EF**: two data-access styles for one small team.
- **A generic repository**: duplicates EF and splits the work by table rather than by use case.
- **`MigrateAsync()` at startup**: races once there is more than one process.
- **One set-based SQL statement for scoring**: faster, but it would hide the scoring rules from tests and from this record.
- **EF's in-memory provider for tests**: runs no triggers, locks or constraints, so tests use real PostgreSQL.

## Consequences

- The job runner's queue statements are plain SQL on a pooled data source, because they must be short and conditional on the attempt.
- The measured time and peak memory of the historic import are recorded here once slice 2 has run.
