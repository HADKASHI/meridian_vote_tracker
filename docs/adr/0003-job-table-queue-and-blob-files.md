# The job table is the queue; uploaded files live in Blob storage

Status: accepted

## Context

A historic import is about 1M rows and must finish within 5 minutes without slowing the screens. Imports of disagreeing files must be applied in upload order, a crash must never lose or duplicate work, and nothing may hang or fail silently. The system runs locally with `docker compose up` and should move to Azure by configuration alone.

## Decision

- Imports and snapshots run as **jobs** (queued, running, done, failed) from one mutable `job` table, in a separate `worker` service built from the same image as the API.
- Workers poll every second and claim with `FOR UPDATE SKIP LOCKED` under an advisory lock. **Imports run one at a time by a setting** (`Imports:MaxConcurrent`, default 1) checked at claim time, so the earlier upload is always the approved one; snapshots run alongside imports, one at a time among themselves.
- A running job **heartbeats** every 10 s; a sweeper requeues one silent for 60 s; after 3 attempts, or a hard time limit per kind, it fails with its error kept. Every update is conditional on the attempt, so a worker that was swept can't touch the job again. A retried import resumes at the first vote it had not committed.
- A job stores the **trace parent** of the request that queued it and runs in that trace, so one trace id finds the upload and every vote transaction.
- **Progress is polled**: the import or snapshot page every second while active, and `GET /api/status` for the header. Nothing pushes.
- Uploaded files go to **Azure Blob Storage** from day one, Azurite locally. The API streams the file to Blob while hashing it, then inserts the import and its job in one transaction. Files are never deleted.

## Considered options

- **Hangfire**: its own schema, and it hides the ordering rule.
- **Service Bus or RabbitMQ**: a second source of truth that needs an outbox to stay consistent with the database.
- **LISTEN/NOTIFY**: saves under a second of pickup delay on jobs lasting seconds to minutes, and still needs the poll for notifications missed while a worker was down.
- **A unique index allowing one running import**: integrity never depended on serial imports (the per-vote lock does that), so a setting keeps parallel imports a configuration change.
- **SignalR or server-sent events**: the worker would have to signal the API first, and `EventSource` can't send the user header.

## Consequences

- A refresh loses no progress: it lives in the database.
- Nobody can cancel a job yet; cancelling a queued import is the next step.
- Direct browser-to-Blob uploads with SAS links are the scale-out path.
