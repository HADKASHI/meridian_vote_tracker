# Spec: Meridian Vote Tracker

Status: ready-for-agent

Synthesised from the wayfinder map (`../meridian-vote-tracker/map.md`) and its 23 resolved tickets. "Map ticket NN" means `../meridian-vote-tracker/issues/NN-*.md`; build tickets live in this folder's `issues/`. Terms follow `GLOSSARY.md`. Requirement IDs (ING, CON, HIS, POS, SNP, PDF) refer to the assignment brief.

## Problem Statement

The Northwind Policy Institute publishes an Alignment Index: a 0–100 score per member state of the Meridian Assembly measuring how often that state voted with the Institute's positions, weighted. Today the data behind it arrives as CSV exports from the Assembly that are messy (typos in country codes, ballot words instead of letters, mixed date formats, duplicates, rows that disagree with each other), that get re-sent with corrections, and that sometimes silently drop votes. Analysts have no way to tell which values are trustworthy, where a value came from, who approved a change, or which data a published index was based on. When a revised file disagrees with what was already accepted, nothing stops it from overwriting approved data, and two analysts can make contradictory decisions at the same time without either knowing. A published index can't be reproduced a year later, because the data under it has moved.

## Solution

A web application (ASP.NET Core API, background worker, PostgreSQL, Angular, run with `docker compose up`) in which:

- Analysts upload Assembly export files. Each import runs as a **job** in the background with live progress, and gives every row exactly one **outcome** (new, unchanged, duplicate, invalid, contradiction, conflict) with a reason, recorded permanently together with the original file.
- Values that differ from **approved data** never overwrite it. They become **conflicts** in a Review inbox, where an analyst accepts or rejects each one with an optional comment. If two analysts decide the same conflict at once, exactly one decision applies and the other sees who won and what they decided.
- Every vote has a page with its details, position, country votes and a full timeline: imports that touched it, every approved-value change with its origin, conflicts opened and decided, losing decision attempts, position and weight changes, and snapshots that used it.
- The admin sets a **position** (yes/no) and **weight** (1–5) per vote.
- Analysts calculate **snapshots** of the Alignment Index over a chosen set of votes. A snapshot stores its full answer (the exact revisions it used, a categorised line per vote and state, and results) and never changes. The admin publishes one per reporting quarter and may replace it with a stated reason, keeping the chain visible.
- History is append-only and enforced by the database, so nothing approved is ever lost or silently rewritten.

## User Stories

### Identity and roles

1. As an analyst, I want to pick myself (Dana or Omar) from a user switcher in the header, so that every action I take is recorded under my name.
2. As the admin (Lena), I want to pick myself from the same switcher, so that I can set positions and publish snapshots.
3. As a user, I want each browser tab to keep its own chosen user across reloads, so that I can act as Dana in one tab and Omar in another.
4. As a user opening a new tab, I want it to start as the user I last chose, so that I don't have to pick again every time.
5. As an analyst, I want admin-only actions to stay visible but disabled with the reason "Admin only", so that I understand what I can't do and why.
6. As the Institute, I want the API itself to refuse unknown users (401) and wrong roles (403), so that roles are enforced even if someone bypasses the UI.
7. As a user whose identity is rejected with a 401, I want the user switcher to open, so that I can pick a valid user and continue.
8. As a reviewer of the code, I want every write endpoint tested as nobody and as the wrong role, so that a new endpoint without a policy can't slip through.

### Uploading and processing imports

9. As an analyst, I want to upload a CSV export through the browser, so that its data enters the system.
10. As an analyst, I want the upload to be accepted immediately with an import id, so that I don't wait while a large file is processed.
11. As an analyst, I want to see the import's job progress (queued with place in queue, parsing with rows read, applying with votes done of total), so that I know what is happening with a large file.
12. As an analyst, I want progress to survive a page refresh, so that I can leave and come back.
13. As an analyst, I want imports processed one at a time in upload order, so that when two files disagree, the earlier upload is the approved one and the later one raises conflicts.
14. As an analyst, I want a job that crashed or stalled to be retried and resumed from the votes already committed, so that a worker restart never loses or duplicates work.
15. As an analyst, I want a job that fails after its retries to say so with the error and how far it got ("failed after 812 of 1,204 votes"), so that a failure is never silent and never hangs forever.
16. As an analyst, I want a failed import not to block later imports in the queue, so that one bad file doesn't stop the team.
17. As an analyst, I want the ~1M-row historic file imported in under 5 minutes, so that loading history is practical.
18. As an analyst, I want the original file kept with its SHA-256, uploader and upload time, so that any outcome can be traced back to the exact evidence.
19. As an analyst, I want a file missing a required column, with invalid UTF-8, or with no data rows to be recorded as rejected with the reason, so that I know why nothing was applied.
20. As an analyst, I want columns matched by header name in any order, case-insensitively, so that a re-ordered export still imports.
21. As an analyst, I want extra columns ignored and noted as a warning, so that harmless format changes don't block an import.
22. As an analyst, I want files with or without a BOM and with LF or CRLF line endings accepted, so that exports from different tools work.
23. As an analyst, I want the title column read as `resolution_title`, as the real files name it, so that real exports import.

### Classification and the import report

24. As an analyst, I want every row to get exactly one outcome, so that the report adds up to the file.
25. As an analyst, I want rows with the wrong field count, a missing required field, an unknown country code, an invalid ballot, an invalid date, a non-positive session, or a state that wasn't a member on the vote date to be invalid with a specific reason, so that I can tell the Assembly what to fix.
26. As an analyst, I want ballot words like `Yes` to be invalid rather than mapped to `Y`, so that the importer never guesses meaning and a later `A` for the same state is treated as new, not as a false conflict.
27. As an analyst, I want an empty ballot to be invalid rather than read as absent, so that missing data is never mistaken for an Assembly-reported absence.
28. As an analyst, I want documented normalisations (trimming, invisible characters removed, codes and single-letter ballots upper-cased, day-first dates in several formats converted to ISO) applied and recorded on each row, so that harmless formatting doesn't create errors and every change is visible.
29. As an analyst, I want ambiguous or impossible dates (month-first, 2-digit years, times, impossible days) to be invalid, so that no date is guessed.
30. As an analyst, I want rows in one file that give one state different ballots in one vote to all be contradictions, so that the importer never picks a side.
31. As an analyst, I want a vote whose rows disagree on date, title or session within one file to be held back entirely as a contradiction with the reason given once, so that no vote is stored with details the importer chose.
32. As an analyst, I want an exact repeat of an earlier row in the same file to be a duplicate while the first copy is classified normally, so that duplicates are reported without losing the value.
33. As an analyst, I want a row matching approved data to be unchanged, so that re-sent data is acknowledged without creating history.
34. As an analyst, I want a row with a value for which nothing is approved yet to be new and approved immediately, so that clean data flows in without manual work.
35. As an analyst, I want a row that differs from approved data to be a conflict that leaves approved data untouched, so that corrections are reviewed by a person.
36. As an analyst, I want differences in country name (Turkey vs Türkiye) and `published_at` ignored when comparing, so that cosmetic differences never become conflicts.
37. As an analyst, I want titles compared ignoring case, spacing and typographic quotes and dashes, but stored as published, so that trivial edits aren't conflicts and the original text is kept.
38. As an analyst, I want a vote-details change to open one conflict per vote rather than one per row, so that rev2 gives me 8 decisions, not 304.
39. As an analyst, I want a row carrying new vote details to still have its ballot compared on its own, so that a ballot change in the same row opens its own conflict and a new ballot is stored under the approved details.
40. As an analyst, I want re-sending a value that is already pending as a conflict to link to the open conflict instead of opening another, so that re-uploads don't flood the Review inbox.
41. As an analyst, I want a value that was already rejected never to reopen as a conflict, so that settled questions stay settled.
42. As an analyst, I want a different incoming value on the same subject to open its own conflict, so that each distinct proposal gets a decision.
43. As an analyst, I want re-uploading the same file to be allowed, warned as "identical to import #N", and show nothing changed, so that I can re-run safely.
44. As an analyst, I want a vote or state missing from a later file to change nothing, so that absence is never treated as a correction.
45. As an analyst, I want a warning per incomplete vote (members on the vote date with no approved ballot after this import), so that I know which data is still missing.
46. As an analyst, I want a warning when a file's `published_at` is older than the newest already imported, so that I notice an out-of-order export.
47. As an analyst, I want the import report to show counts per outcome as filters, the warnings, the conflicts opened, and every row record paged (line number, raw text, outcome, reason, normalisations, links), so that I can audit any line.
48. As an analyst, I want counts per vote in the report, so that I can see where problems cluster.
49. As an analyst, I want a list of all imports with uploader, time, job state and outcome counts, so that I can find any past import.

### Conflicts and the Review inbox

50. As an analyst, I want a Review inbox listing pending conflicts (vote, subject, current → incoming, import), with a vote's conflicts adjacent, so that I can work through them in order.
51. As an analyst, I want a header badge with the pending conflict count, so that I know when there is review work.
52. As an analyst, I want a decision pane that shows both values with their origins (file, line, uploader, or the decision that approved the current value), so that I decide on evidence.
53. As an analyst, I want to accept or reject (keep current) a conflict with an optional comment, so that my reasoning is recorded.
54. As an analyst, I want accepting a conflict to make the incoming value the approved value, with the decision as its origin, while the previous value stays in history, so that nothing is lost.
55. As an analyst, I want a conflict decided against the current value at the time I decide, so that a conflict opened before another decision is still judged correctly.
56. As an analyst, I want deciding one conflict never to rewrite another, so that each conflict's record stays what it was.
57. As an analyst, I want that if Omar and I decide the same conflict simultaneously, exactly one decision applies and the other gets the winner's decision (who, when, accept/reject, from → to, comment) in place of the buttons, so that nobody's decision is silently overwritten.
58. As an analyst who lost such a race, I want to be told my decision was not applied and that the attempt is on the vote's timeline, so that I know what happened and it's on record.
59. As an analyst, I want "Next conflict" to move on after a decision or a lost race, so that I keep working.
60. As an analyst, I want every vote-details decision to recalculate membership for that vote, so that a corrected date counts the right states.
61. As an analyst, I want ballots that were invalid only because of membership, and that pass after a date correction, to be approved with the decision as origin, so that a date fix doesn't strand valid ballots.
62. As an analyst, I want approved ballots now outside membership to stay stored but stop counting, so that history is kept and scores are right.
63. As an analyst, I want decided conflicts to stay viewable, so that past decisions can be reviewed.
64. As an analyst, I want imports and decisions on the same vote never to interleave, so that neither a bogus conflict nor a missed conflict can arise.

### Vote page and timeline

65. As an analyst, I want to search votes by assembly vote id, title, session, date and "no position", so that I can find any vote.
66. As an analyst, I want a vote page with its approved details, position and weight, and every country vote, so that I see its current state at a glance.
67. As an analyst, I want each approved value to show its origin (the import row that brought it, or the decision that approved it), so that I can answer "which file, who uploaded it, who approved it".
68. As an analyst, I want a timeline per vote showing imports that touched it with per-vote counts, approved-value changes as from → to with origin, conflicts opened and decided, losing attempts, position and weight changes, and snapshots and publications that used it, so that I can tell the vote's whole story.
69. As an analyst, I want the timeline ordered by a single sequence rather than timestamps, so that order is exact even when times tie.
70. As an analyst, I want the timeline paged, so that a heavily revised vote still loads quickly.
71. As a client developer, I want from → to pairs computed by the API, so that every client shows the same history.

### Positions and weights

72. As the admin, I want a positions worklist of votes with stance and weight, votes without a position first, so that I can see what still needs a position.
73. As the admin, I want to set a vote's stance (yes/no) and weight (1–5) inline, so that the index reflects the Institute's view.
74. As an analyst, I want to read positions but not edit them, so that only the head of research sets policy.
75. As the admin, I want each position change recorded with who, when and from → to, so that policy changes are auditable.
76. As the admin, I want a stale save (someone changed the position while I edited) to be refused with the current value shown and "Use theirs" or "Save mine over it", so that I never overwrite a change I didn't see.

### Snapshots

77. As an analyst, I want to create a snapshot with a required name and optional note, so that I can label calculations.
78. As an analyst, I want to select votes by date range (inclusive, on approved dates), by one or more sessions, or by an explicit list, so that I can index exactly the votes I mean.
79. As an analyst, I want a preview showing the resolved vote count before calculating, so that I know what I'm about to calculate.
80. As an analyst, I want a selection that matches no votes refused up front, so that empty snapshots aren't created.
81. As an analyst, I want votes with no position to block the snapshot, listed in the preview, so that no snapshot is ever partial over the votes it names.
82. As an analyst, I want "Remove these votes" to turn my selection into the rest, recorded as "range minus these votes", so that I can proceed knowingly and the snapshot says what was left out.
83. As an analyst, I want "Check again" after the admin sets missing positions, so that I can proceed without rebuilding my selection.
84. As an analyst, I want warnings (pending conflicts on selected votes, incomplete votes, an import currently running) shown in the preview but not blocking, so that I decide whether to wait.
85. As an analyst, I want a snapshot to be refused at calculation time if a vote without a position slipped in after the preview, naming the vote and why, so that the blocking rule always holds.
86. As an analyst, I want snapshots calculated as a background job that never waits behind a long import, so that I can calculate while files load.
87. As an analyst, I want a snapshot calculated over one consistent view of the data, with any running import recorded as a warning ("calculated while import #12 was applying, 812 of 1,204 votes"), so that I know exactly what it saw.
88. As an analyst, I want a snapshot's ranking showing each state's score to 1 decimal with its basis (countable votes and counted weight), so that thin evidence is visible.
89. As an analyst, I want abstentions, absences, votes outside membership and votes with no data excluded and labelled separately, so that the score reflects only yes/no votes and the reasons are clear.
90. As an analyst, I want a state with nothing countable shown as n/a, unranked and listed last, so that it isn't mistaken for a score of 0.
91. As an analyst, I want exact ties to share a rank (1, 2, 2, 4), ordered by code, so that ranking is fair and deterministic.
92. As an analyst, I want a per-country breakdown listing every vote with its category (match, mismatch, abstain, absent, non-member, no data) and the exact value used, so that every score can be recomputed by hand.
93. As the Institute, I want a snapshot to store its votes, lines and results with the exact revisions used and the scoring rules version, and never recompute them, so that it reads the same in a year even after data or code changes.
94. As an analyst, I want a failed snapshot shown as failed with its error, so that I can retry.
95. As an analyst, I want a snapshots list with name, creator, time, vote count and publication badge, so that I can find the right one.

### Publication

96. As the admin, I want to publish a snapshot for a reporting quarter I choose (defaulting to the current quarter), so that an index of Q1 votes can be published in April.
97. As the admin, I want to replace a quarter's published snapshot with another, giving a required reason, so that errors can be corrected openly.
98. As anyone, I want superseded publications to stay visible as "superseded by …, reason …", so that the record of what was published is complete.
99. As the admin, I want two simultaneous publications for one quarter to result in one success and one 409 showing the quarter's new publication with "Replace it", so that a quarter never has two official indexes.
100. As anyone, I want a publications page showing each quarter's chain, so that I can see what the official index is and was.
101. As anyone, I want to answer "which data was the published index based on" from the snapshot, so that the Institute can defend it.

### Snapshot comparison (extra)

102. As an analyst, I want to compare two snapshots and see each state's rank and score movement, and the votes the two treated differently that explain it, so that I can explain changes between quarters or between a publication and its replacement.

### Snapshot drift (extra)

103. As an analyst, I want a snapshot's page to tell me what would differ if I created it now (positions, country votes and vote details that changed, votes that now match its selection and votes that no longer do), without recalculating anything, so that I can tell whether a published index is still current.

### Jobs

103. As a user, I want a header indicator ("2 jobs running · 1 queued") linking to a Jobs list, so that I always know what the system is doing.
104. As a user, I want the Jobs list to show every job's kind, state, place in queue, progress and failure reason, so that I can see why something is waiting or failed.
105. As a user who started a job, I want one notice when it finishes or fails, with a link, so that I don't have to watch it.
106. As a user, I want the words queued, running, done and failed used identically in the UI, API and code, so that nobody has to translate.

### Errors, tracing and operations

107. As a user, I want form errors returned by the API shown on the right fields, so that I can fix my input.
108. As a user, I want unexpected errors shown with a reference (trace id), so that support can find what happened.
109. As an operator, I want one trace id to follow an upload through the API, the job and every vote transaction, so that I can debug a whole import from one id.
110. As the Institute, I want logs to carry ids, counts and timings but never client data (raw rows, titles, ballots, comments), so that logs are safe to ship.
111. As an operator, I want Application Insights to switch on only when its connection string is configured, so that the app runs locally without Azure.
112. As a reviewer, I want `docker compose up --build` on a clean clone (with `member_states.csv` in `candidate_data/`) to start the whole system, so that I can evaluate it without setup.
113. As a reviewer, I want `migrate` to fail with a clear message naming the missing member states file, so that a setup mistake is obvious.
114. As a reviewer, I want it to work with no `.env` file, so that there is nothing to configure.
115. As an operator, I want uploaded files stored in Azure Blob (Azurite locally) through configuration only, so that moving to real Azure is a connection string.
116. As the Institute, I want assignment data never committed, baked into images or written to logs, so that client data stays private.
117. As a developer, I want the API contract in a committed OpenAPI file and the Angular client generated from it, so that client and server can't drift.
118. As a developer, I want CI to build, test, check the contract and migrations, and smoke-test the compose stack on every push, so that main always works.

## Implementation Decisions

Detailed reasoning lives in the resolved map tickets; the locked schema is in `../meridian-vote-tracker/research/16-schema.md` (to become the schema document in `docs/` at the start of the build).

### Stack and runtime

- .NET 10 LTS, ASP.NET Core with the built-in OpenAPI 3.1 document (emitted at build time and committed) and ProblemDetails errors; JSON number handling strict. PostgreSQL 18 with the Npgsql EF Core provider and snake_case naming. Angular 22 (standalone, zoneless, signals), Angular Material 22, orval-generated client (`httpResource` for reads, `HttpClient` services for writes). Node 24.
- One .NET solution and one backend image with three entry points: `migrate` (EF migration bundle plus member-state seeding), `api`, `worker`. Six compose services: `db`, `azurite`, `migrate`, `api`, `worker`, `web` (nginx serving the SPA and proxying `/api/` on the same origin; no CORS). Health-checked start order; host ports bound to localhost only; works with no `.env`.
- Configuration keys: `ConnectionStrings:Meridian`, `Blob:ConnectionString`, `Blob:Container`, `Imports:MaxConcurrent` (1), `Imports:MaxUploadBytes` (200 MB), `Jobs:PollInterval` (1 s), `Jobs:HeartbeatInterval` (10 s), `Jobs:StaleAfter` (60 s), `Jobs:MaxAttempts` (3), `Jobs:ImportTimeout` (30 min), `Jobs:SnapshotTimeout` (10 min), `Seed:MemberStatesPath`.

### Identity and authorization

- A custom ASP.NET authentication handler reads `X-User-Id` and produces a principal with role claims from the seeded users (`dana`, `omar` analyst; `lena` admin). Two policies: `Analyst` (both roles) and `Admin`. Replacing it with real sign-in touches only the handler.
- `GET /api/users` feeds the switcher. An Angular interceptor adds the header to every `/api/` call.

### History model (ADR: typed append-only revisions)

- Three insert-only revision histories: country vote, vote details, position. Two pointer tables (current vote: details and position revision; current country vote) are the only rows that move. Every revision has exactly one origin: the import row that brought it or the conflict decision that approved it (positions record who set them).
- A database guard trigger refuses UPDATE, DELETE and TRUNCATE on every table except `job`, the two pointer tables and the migrations history. Reference tables are guarded too.
- One shared history sequence supplies the id of every guarded table, so the timeline orders by id across tables.
- A vote row is created the first time a well-formed assembly vote id is seen (even if the whole vote is a contradiction); it is the per-vote lock target.
- Views (raw SQL in migrations, mapped as keyless entities): current country vote, current vote, pending conflict, and a vote timeline (`UNION ALL`, paged by history id). From → to pairs are built in C#.
- Integrity values are text with CHECK constraints; reason codes and warning kinds are C# enums stored as text with no database CHECK.

### Import pipeline (ADR: import classification and conflicts)

- Upload: the API streams the file to Blob (`imports/{importId}/{filename}`) while hashing, then inserts the import and its job in one transaction and returns 202 with the import id. Files are never deleted.
- Worker: parse the whole file, group rows by vote in memory, classify each vote with the pure classification module, then persist one vote per transaction through the `ImportWriter` seam, taking `SELECT … FOR UPDATE` on the vote row first. Parse-time facts go in a one-per-import parse record; warnings in an insert-only warning table.
- Classification is a pure function over a vote's parsed rows plus the approved values and pending conflicts for that vote. Rule order: invalid → contradiction → duplicate → conflict / unchanged / new. Normalisation and comparison-key rules exactly as map ticket 08 rule 2. Membership is inclusive at both ends; an empty end date means still a member.
- Conflicts: one table for both subjects (ballot, details). A conflict stores the incoming value and a link to the revision it was compared against; current is read live. Partial unique indexes on (compared-against revision, incoming value) per subject, so a matching incoming value links to the existing conflict, pending or decided (reason `previously_rejected` when decided as reject). A row links to up to two conflicts.
- `ImportWriter` is built with EF (`AddRange` + `SaveChanges` per vote, change tracker cleared). Budget: the ~1M-row historic import under 5 minutes. Only if missed do its internals switch to Npgsql binary COPY; classification is untouched. If peak worker memory exceeds about 1 GB, the parser switches to two passes.
- Crash resume: a retried import skips votes that already have row records for this import.

### Conflict decisions

- A decision is an insert-only record, unique per conflict; the database rejects a second. The decision module locks the vote, inserts the decision, and on accept inserts the new revision (origin: the decision) and moves the pointer, all in one transaction. A decision records the revision it replaced.
- The loser of a race gets 409 `already-decided` with the winning decision in the body, and a not-applied attempt record is inserted.
- Every vote-details decision recalculates membership for that vote (map ticket 08 rule 8).

### Positions

- The version is the position revision id. A save inserts a revision and moves the pointer conditionally (`IS NOT DISTINCT FROM` the seen id); zero rows updated means 409 `stale-position` with the current value.

### Snapshots and publication (ADR: snapshots store their answer)

- `POST /api/snapshots/preview`: a cheap query (no job) returning resolved vote count, the blocking `no-position` list, and warnings.
- Create: refuses (400) no matching votes and votes without a position; inserts the snapshot header and its job.
- Job: in one `REPEATABLE READ` transaction, stream facts (state, vote, ballot or none, member on date, stance, weight) from the current views, re-check positions, apply the pure scoring module, and write snapshot votes (with details and position revision ids), lines (with country vote revision id, or null for no data, and category), results (matched and counted weight as integers, rank or null) and warnings. The header records the scoring rules version.
- Scoring: score = 100 × matched ÷ counted over yes/no ballots of members; abstain, absent, non-member and no-data excluded and labelled; counted 0 means n/a and no rank; competition ranking on the exact value; ties ordered by code. The database checks `matched ≤ counted` and that rank is null exactly when counted is 0.
- Publication: an insert-only chain (snapshot, quarter `YYYY-Qn`, who, when, reason, supersedes). Partial unique indexes allow one first publication per quarter and one successor per publication; a reason is required exactly when superseding. A unique violation becomes 409 `quarter-published`.
- Drift (extra): `GET /api/snapshots/{snapshotId}/changes` for a calculated snapshot compares the stored position, details and country vote revision ids with the current ones, and re-resolves the saved selection against today's votes. It returns, per kind (position changed, country votes changed, details changed, now selected but not in the snapshot, no longer selected), the real count and the first 20 votes with the old and new value, who or what changed it and when, plus the time it was read. It never reads or returns a score or rank and runs no job. Pending conflicts are not changes.
- Comparison (extra): a pure read over two snapshots' stored results, lines, votes and the ballots they used. `GET /api/snapshots/compare` returns each state's rank and score in A and B with their basis and the change, and its **reasons**: the votes whose matched or counted weight differs (vote only in one snapshot, a different ballot, a different stance or weight). The reasons are in the same response, not a second endpoint.

### Jobs (ADR: job table as queue, Blob for files)

- One mutable `job` table for kinds `import` and `snapshot` (later `pdf_extraction`): state `queued` → `running` → `done` | `failed`, progress counters (rows read, votes done, votes total), heartbeat, attempts, last error, `trace_parent`, timestamps.
- Workers poll every second and claim with `FOR UPDATE SKIP LOCKED`. Imports are limited by `Imports:MaxConcurrent` checked under an advisory lock at claim time; snapshots run alongside imports, one at a time among themselves.
- Heartbeat every 10 s; a sweeper requeues jobs silent for 60 s; after 3 attempts the job fails. Hard timeouts per kind.
- Progress is polled: import and snapshot resources every 1 s while active; `GET /api/status` (running and queued jobs, recently finished, pending conflict count) drives the header. No SSE or SignalR, no cancel.

### API error contract

- Every error is ProblemDetails with `traceId`. Fixed `type` values with typed bodies in the OpenAPI document: validation (field keys are camelCase JSON paths), `no-votes-match` (400), `no-position` (400, votes), `already-decided` (409, decision), `stale-position` (409, current), `quarter-published` (409, publication), `unknown-user` (401), `forbidden` (403). The UI maps them one to one; 409s replace the action in place, never a toast or automatic retry.

### Angular app

- Lazy feature routes: `/imports`, `/imports/:importId`, `/review`, `/review/:conflictId`, `/votes`, `/votes/:voteId` (`?tab=timeline`), `/positions`, `/snapshots`, `/snapshots/new` (preview dialog), `/snapshots/:snapshotId`, `/publications`, `/jobs`, `/snapshots/compare` (extra). `/` redirects to `/review`. Filters and tabs live in query params. Internal ids in URLs; assembly vote ids are displayed only.
- Feature folders import only core, shared and the generated API (lint-enforced). State is signals and `httpResource`; no NgRx. Review inbox is a split pane; list items carry a `kind` so other item kinds can join.
- Display: ISO dates, scores to 1 decimal with basis, glossary words exactly.

### Observability

- W3C trace context started by the server; nginx passes `traceparent` through. A job stores its request's trace parent and runs as a child span. Built-in JSON console logging with scopes (user; job, import or snapshot, vote). Azure Monitor OpenTelemetry only when its connection string is set. No client data in logs.

### Build order and slices

The build is cut into **slices**: vertical pieces (API, worker, screen and tests together), each delivered as one branch and one PR into `main`. Each slice has a brief at `slices/NN-<slug>/slice.md` beside this spec, and its tickets live under that slice. Slice 0 is the **shared base**: it fixes everything the other slices would otherwise collide on, so that slices 1, 3, 4, 5 and 6 can be built at the same time in separate sessions.

0. **Walking skeleton (shared base)**, built and merged alone:
   - compose with all six services; the whole locked schema as migration 1, with the guard test; seeded users and member states
   - the auth handler, `Analyst` and `Admin` policies, `GET /api/users`, and the generated role-matrix test
   - the **full API contract**: every endpoint of every slice with its final route, request and response DTOs and problem types, returning 501 "not implemented"; the committed OpenAPI file and the orval client generated from it
   - one **feature module** per area on the backend (imports, conflicts, votes and timeline, positions, snapshots and publication), each with its stubbed endpoints and its own service registration, already wired into startup, so a slice never edits startup or another module
   - the **generic job runner**: claiming with `SKIP LOCKED`, the `Imports:MaxConcurrent` check, heartbeat, sweeper, retries, timeouts, `trace_parent`, and `GET /api/status`, proven with a no-op job; slices 1 and 6 only plug in their job handler
   - the **Angular shell**: user switcher and identity interceptor, every route with a placeholder page in its feature folder, the header jobs indicator and Review badge, the Jobs list, the shared ProblemDetails helpers (`problemToFieldErrors`, error notices with `traceId`, the 409 card) and `pollWhile`
   - the **test base**: both test projects, Testcontainers PostgreSQL and Azurite, the template database per test, the on-demand job processor, the barrier helper for race tests, and **fixture builders** that write approved values (with a fixture import as origin), conflicts, positions and snapshots straight into the database, so any slice can arrange its data without another slice's code; the same builders are reachable from a Development-only `seed-demo` command for checking screens by hand
   - tracing and JSON logging, CI (three jobs), the six ADRs from today's DECISIONS.md text, the schema document
1. **Import** (ING): upload, import job handler, classification, `ImportWriter`, crash resume, report screen. The brief's 7.2 example is the required integration test.
2. **Import at scale**: the timed synthetic ~1M-row test (5 minutes, peak memory); COPY or the two-pass parser only if needed.
3. **Conflicts** (CON): Review inbox, decisions, membership recalculation, the decision race test.
4. **Vote page and timeline** (HIS).
5. **Positions and weights** (POS), with the stale-position race test.
6. **Snapshots and publication** (SNP): preview, snapshot job handler, breakdown, publish and replace, the publication race test.
10. **Interface**: one finished look across every screen, built from Angular Material and matched to the brief's Appendix B and the map ticket 19 prototype, with no behaviour change. Added after the first slices shipped screens that work but are barely designed; numbered 10, run before 7 and 8.
7. **Extra: snapshot comparison.**
11. **Extra: snapshot drift**: a summary on a snapshot's page of what would differ if it were created now, added after slice 7 shipped.
8. **Finishing pass** (protected): clean-clone run, README, DECISIONS.md rewritten to ~1,000 words, AI_USAGE.md, skills trim, and a Playwright smoke test across the merged slices.
9. **Bonus: PDF extraction**: designed first, then built, with a hard half-day limit.

**Dependencies** (a slice starts once every slice it is blocked by is merged into `main`):

| Slice | Blocked by | Runs in parallel with |
|---|---|---|
| 0 Walking skeleton | none | nothing |
| 1 Import | 0 | 3, 4, 5, 6 |
| 2 Import at scale | 1 | 3, 4, 5, 6, 10 |
| 3 Conflicts | 0 | 1, 2, 4, 5, 6 |
| 4 Vote page and timeline | 0 | 1, 2, 3, 5, 6 |
| 5 Positions and weights | 0 | 1, 2, 3, 4, 6 |
| 6 Snapshots and publication | 0 | 1, 2, 3, 4, 5 |
| 10 Interface | 4, 5, 6 | 2 |
| 7 Snapshot comparison | 6, 10 | 2 |
| 11 Snapshot drift | 6, 7 | 8 |
| 8 Finishing pass | 0–7, 10 | nothing |
| 9 PDF extraction | 8 | nothing |

Slices 3, 4 and 6 arrange their data with slice 0's fixture builders, never with the importer, so they don't wait for slice 1. Once slice 1 is merged, any slice still open rebases and may add one end-to-end test through a real import.

**Cut rule**: if time runs short, the PDF bonus goes first, then the drift and comparison extras, then the interface slice. The finishing pass is never cut.

### Delivery

- **One slice, one session, one branch, one PR into `main`.** The PR title is `Slice N: <name>`. Its body is written with the `pr` skill and lists the requirement IDs covered, the tickets closed and the tests that prove them.
- **Merged by rebase-merge**, so every commit of the slice lands on `main` as written. When several slices are ready together, merge them in build order. A slice branch rebases onto `main` and goes green again before merging.
- **Commits tell the slice's story**: the slice's tickets first, then roughly one commit per ticket with its tests inside, plus a few small fix-ups at most. Each has an imperative summary of 60 characters or fewer and a short body saying why. A commit that is pushed stays as it is; a fix to it is its own small commit that explains itself.
- **A slice stays in its lane.** It fills in its own feature module, its own Angular feature folder and its own tests. It changes only its own endpoints in the contract, and regenerates the OpenAPI file (never resolving it by hand) after a rebase. Parallel slices add no migrations; if one is unavoidable, the slice stops and says so in its PR, so that only one migration is ever in flight.
- **Tests belong to their slice**, and a slice is done when its PR is green in CI with every acceptance criterion of its tickets ticked.

## Testing Decisions

- **What makes a good test**: it exercises external behaviour through a public seam (an HTTP endpoint, a pure function's inputs and outputs, a database constraint) and asserts on outcomes a user or the brief cares about: outcome counts, approved values, status codes and problem types, scores and ranks. It never asserts on EF internals, private methods or markup snapshots. Each numbered rule in map tickets 05 and 08 gets a named test.
- **Seams** (from map ticket 20; existing seams, no new ones):
  1. **The HTTP API** through `WebApplicationFactory`, against real PostgreSQL and Azurite in Testcontainers (same versions as compose). This is the main seam: imports, conflicts, timeline, positions, snapshots and publication are tested here end to end. Jobs are run on demand by calling the same job processor ("run queued jobs until none are left").
  2. **Pure domain functions**: parsing, normalisation, classification and scoring, unit-tested with no database, written test-first.
  3. **Use-case modules and the database** directly, only where the API can't reach: the guard (catalog query that every non-allowlisted table has the trigger, then UPDATE, DELETE and TRUNCATE refused), and crash resume through the real `BackgroundService`.
- **Projects**: a domain test project and an integration test project, xUnit v3, built-in `Assert` (no FluentAssertions).
- **Database isolation**: one PostgreSQL container per run; migrations and seeding run once into a template database; each test gets a fresh `CREATE DATABASE … TEMPLATE`. The guard is never disabled; TRUNCATE-based resets are ruled out by design.
- **Required and fixed tests**:
  - The brief's 7.2 example through the API: upload → job → report, then outcome counts, approved values and the conflict.
  - Role matrix generated from endpoint metadata: every write endpoint as nobody (401) and as the wrong role (403).
  - Races for conflict decision, publication and stale position: two connections released by a barrier, 20 rounds, exactly one success and one 409 per round.
  - Validation error keys are camelCase JSON paths.
  - Guard coverage as above.
- **Categories run on demand, never in CI**: `RealData` (real Q1 files give the audited counts: original 5,737 new / 3 duplicate / 5 invalid / 151 contradiction; rev2 372 new / 5,284 unchanged / 304 conflict rows / 8 conflicts; rev2 again opens none; historic predicts 962,109 new / 30 duplicate / 32 invalid), skipping itself when `candidate_data/` is missing; `Performance` (deterministic synthetic ~1M-row file shaped like the historic one, fails above 5 minutes, reports time and peak memory), also available as a manual workflow.
- **Frontend**: Vitest with Material harnesses and `HttpTestingController` for the identity interceptor, ProblemDetails-to-field mapping, the 409 cards and stopping polling. One Playwright smoke test against the compose stack, added in slice 8 once slices 1 and 3 are merged: switch user, upload a small file, see the report, then a decision race in two tabs.
- **Test data**: committed synthetic fixtures only (one small CSV per import rule, synthetic member states, C# builders). Real data is never committed.
- **CI on every push**: build with warnings as errors, OpenAPI drift check, pending-model-changes check, domain and integration tests, Angular lint/test/build, compose smoke test. Prior art: none yet in the codebase (it has no code); the audit scripts in `../meridian-vote-tracker/research/07-data-audit-scripts/` and `../meridian-vote-tracker/research/17-historic-audit-scripts/` are the reference for expected counts.

## Out of Scope

- **Bonus PDF extraction (brief section 8)**: not designed on the map. It is a last, optional phase only after the finishing pass is done, with a hard half-day limit; otherwise DECISIONS.md describes its design. The job table and Review inbox already allow a `pdf_extraction` job kind and other inbox item kinds.
- Real authentication (Entra ID, JWT); only the handler would change.
- Cancelling a queued import; push progress (SSE, SignalR); direct browser-to-Blob uploads with SAS links. Each is noted as a next step in DECISIONS.md.
- Unpublishing a snapshot without a replacement; removing a position once set.
- Deleting uploaded files or cleaning up orphaned blobs.
- A database-level overlap constraint on membership periods (checked in C#).
- Running the app as a non-owner database role (listed as a before-production item).
- Custom metrics and a local trace dashboard.
- Image publishing and deployment to Azure.
- Which skills, `.scratch/` notes and sandbox files ship with the submission (decided in the finishing pass).

## Further Notes

- The brief's ING-04 says `dd-mm-yyyy` while Appendix A and every real file use `yyyy-mm-dd`; both are accepted (day-first for numeric dates), per map ticket 08.
- The real title column is `resolution_title`, not `title` (correction from map ticket 17).
- Session numbers are validated as positive integers consistent within a vote, never against the year.
- The sandbox needs a proxy override for container builds; it lives in a separate compose file passed explicitly with `-f`, never in the committed Dockerfiles or `compose.yaml`, and the README never mentions it.
- DECISIONS.md must stay within 2 pages (~1,000 words, no code, tables or diagrams), with the full reasoning in six ADRs and a four-developer plan built on the 501-stubbed skeleton (owners: A import and scale, B conflicts and Review inbox, C vote page, timeline, positions, D snapshots and publication).
- Container reclaims wipe the gitignored `candidate_data/`; real-data tests and the compose run need those files re-uploaded in a fresh session.
