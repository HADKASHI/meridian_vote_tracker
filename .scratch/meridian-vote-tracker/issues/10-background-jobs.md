# Design background jobs and live progress

Type: grilling
Status: resolved
Blocked by: 09

## Question

_Already decided in [Decide concurrency and roles](06-concurrency-and-roles.md): imports are applied one at a time, in upload order, by a single worker, one vote per transaction._

How do imports (ING-01, ING-02) and snapshot calculations (SNP-02) run in the background, with progress that survives a page refresh and jobs that never hang forever? Options: a hosted worker plus a jobs table in PostgreSQL (SKIP LOCKED) vs Hangfire vs a separate worker container; polling vs server-sent events for progress; where uploaded files are kept (local volume vs Azure Blob); restart and crash recovery.

## Comments

- From [Choose how data is read and written](09-data-access.md): schema migrations run in a one-shot `migrate` compose service that every app process waits on, so a separate worker container is not ruled out by migrations. The import worker calls classification (pure) and then `ImportWriter`, one vote per transaction under `SELECT … FOR UPDATE` on the vote. Performance budget: the historic ~1M-row import in under 5 minutes.

## Answer

Decided with the user in three grilling rounds. The user overturned two proposals: serial imports enforced by a database index (Q4), and "job" as an internal-only word (Q14).

1. **The `job` table is the queue.** It is a PostgreSQL table, claimed with `FOR UPDATE SKIP LOCKED` by our own `BackgroundService`. Rejected: Hangfire (its own schema, and it hides the ordering guarantee); Service Bus or RabbitMQ (a second source of truth that needs an outbox; Azure Storage Queue consumers poll anyway).
2. **A separate `worker` compose service**: same solution and image, a different entry point, waiting on `migrate` like `api`. A 1M-row import uses the worker's CPU and memory, never the API's.
3. **One changeable `job` table for every kind** (`import`, `snapshot`, later `pdf_extraction`): a link to its import or snapshot, state (`queued` → `running` → `done` | `failed`), progress counters, `heartbeat_at`, attempt count, last error, and start and finish times. `import` and `snapshot` stay insert-only; the history guard does not cover `job`.
4. **Imports run one at a time by default, as a setting, not a constraint.** `Imports:MaxConcurrent` (default 1) is enforced when a job is claimed: inside one short transaction, take an advisory lock, count running imports, and claim the oldest queued import if under the limit. That is safe with several worker replicas. There is no database index: serial order is a policy (it makes the earlier upload the approved one when two files disagree), and integrity already rests on the per-vote lock and the one-pending-conflict-per-incoming-value constraint. A failed import never blocks the queue.
5. **Crash recovery resumes.** A retried import skips votes that already have row records for this import (`SELECT DISTINCT vote id FROM import_row WHERE import_id = …`); the committed votes are the checkpoint. Classification is idempotent, so the result matches an uninterrupted run.
6. **Never hang**: a heartbeat every 10 s. A sweeper puts a job with no heartbeat for over 60 s back on the queue. After 3 attempts the job fails, with the error shown and the votes applied so far stated ("failed after 812 of 1,204 votes"). Hard ceilings: 30 min per import, 10 min per snapshot. All values are configurable.
7. **Progress is polled**: `GET` on the import or snapshot every 1 s while queued or running, stopping when done or failed. A refresh just resumes polling. A webhook can't reach a browser. Push (SignalR or SSE) would need the separate worker to signal the API first, and `EventSource` can't send the user-switcher header. This goes in DECISIONS.md as a future option.
8. **Progress shape**: queued ("2 imports ahead"); parsing (rows read); applying (votes done of total votes, plus rows); then the report. Votes are the unit of commit, so the count never runs ahead of what is saved.
9. **Files go to Azure Blob from the start**, through the Blob SDK only. Compose runs **Azurite**, and the container is created on startup if missing. Real Azure is just a connection string in `.env` or user-secrets. Integration tests start Azurite through Testcontainers alongside PostgreSQL.
10. **Upload path**: the browser posts to the API, which streams to Blob while computing SHA-256, then inserts the import and its job in one transaction and returns 202 with the import id. Direct browser-to-Blob with a SAS link is recorded as the scale-out path. Files are named `imports/{importId}/{original filename}` and are never deleted; a file left behind by a failed transaction stays, with no clean-up job.
11. **Memory**: the whole file is grouped by vote in memory, as rule 14 of [Define import classification rules](08-import-classification.md) requires; the upload limit is 200 MB. The timed synthetic 1M-row test records the worker's peak memory. If it goes over about 1 GB, the parser switches to two passes (an index of vote id to line offsets, then a re-read per vote), with classification untouched.
12. **Snapshot jobs run alongside imports**, one snapshot at a time among themselves, so a snapshot never waits behind a 5-minute import. Whether to warn or block when one starts during an import is left to [Design snapshots](11-snapshots.md).
13. **Workers check the table every second**, with no LISTEN/NOTIFY. NOTIFY would still need the check for notifications missed while the worker was down, plus a dedicated connection and reconnect handling, to save under a second.
14. **No cancel** in this build. "Cancel a queued import" goes in DECISIONS.md as a next step.
15. **"Job" is ubiquitous language** (added to `GLOSSARY.md`): the same four words (queued, running, done, failed) in the C# enum, the API, and the UI. Each import or snapshot page shows its job ("Job: running, 812 of 1,204 votes"). The header shows a "2 jobs running" indicator linking to a Jobs list, which also shows the place in the queue.
