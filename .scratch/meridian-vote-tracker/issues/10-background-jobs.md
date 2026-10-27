# Design background jobs and live progress

Type: grilling
Status: claimed
Blocked by: 09

## Question

_Already decided in [Decide concurrency and roles](06-concurrency-and-roles.md): imports are applied one at a time, in upload order, by a single worker, one vote per transaction._

How do imports (ING-01, ING-02) and snapshot calculations (SNP-02) run in the background, with progress that survives a page refresh and jobs that never hang forever? Options: a hosted worker plus a jobs table in PostgreSQL (SKIP LOCKED) vs Hangfire vs a separate worker container; polling vs server-sent events for progress; where uploaded files are kept (local volume vs Azure Blob); restart and crash recovery.

## Comments

- From [Choose how data is read and written](09-data-access.md): schema migrations run in a one-shot `migrate` compose service that every app process waits on, so a separate worker container is not ruled out by migrations. The import worker calls classification (pure) and then `ImportWriter`, one vote per transaction under `SELECT … FOR UPDATE` on the vote. Performance budget: the historic ~1M-row import in under 5 minutes.
