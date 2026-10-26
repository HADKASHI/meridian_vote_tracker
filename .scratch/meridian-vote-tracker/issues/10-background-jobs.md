# Design background jobs and live progress

Type: grilling
Status: open
Blocked by: 09

## Question

How do imports (ING-01, ING-02) and snapshot calculations (SNP-02) run in the background, with progress that survives a page refresh and jobs that never hang forever? Options: a hosted worker plus a jobs table in PostgreSQL (SKIP LOCKED) vs Hangfire vs a separate worker container; polling vs server-sent events for progress; where uploaded files are kept (local volume vs Azure Blob); restart and crash recovery.
