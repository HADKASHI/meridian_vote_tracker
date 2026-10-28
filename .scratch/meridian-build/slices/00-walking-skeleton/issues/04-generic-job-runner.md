# 04: Generic job runner and status

**What to build:** the worker runs jobs from the `job` table. It claims the oldest queued job with `FOR UPDATE SKIP LOCKED`, honouring `Imports:MaxConcurrent` under an advisory lock and running snapshots one at a time alongside imports; it heartbeats while running, a sweeper requeues jobs silent past `Jobs:StaleAfter`, a job fails after `Jobs:MaxAttempts` or its kind's timeout with the error kept, and each job runs as a child span of the `trace_parent` stored when it was queued. Slices plug in a handler per job kind. `GET /api/status` returns running and queued jobs (with place in queue), recently finished jobs and the pending conflict count; `GET /api/jobs` lists jobs. Proven with a no-op job handler available only to tests.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] A queued no-op job run through the on-demand processor reaches `done` and shows in `GET /api/status` while queued and after it finishes
- [ ] With `Imports:MaxConcurrent` at 1, a second import job is not claimed while one is running; a snapshot job is
- [ ] A running job whose heartbeat is older than `Jobs:StaleAfter` is requeued by the sweeper, and fails with its last error after `Jobs:MaxAttempts`
- [ ] A handler that throws leaves the job `failed` with `last_error` once attempts run out, and a failed job doesn't block the next one
- [ ] Killing the real `BackgroundService` mid-job and starting another leaves the job requeued and then `done`
- [ ] A job stores the `trace_parent` of the request that queued it and runs in that trace
