# Research: comparing history storage patterns

Ticket: `issues/14-compare-history-patterns.md` (feeds `issues/15-choose-history-pattern.md`).
Researched: 2026-10-27. Facts about PostgreSQL, EF Core, Npgsql and libraries cite the primary source they were read from on that date. Statements marked **(analysis)** are my reasoning or estimates, not documented facts.

Access notes: `postgresql.org` and `learn.microsoft.com` returned proxy 403 (as in ticket 02). The same text was read from the repos those sites are built from: `postgres/postgres` `doc/src/sgml` (branch `REL_18_STABLE`, plus `REL_19_STABLE` and `master` for "is it coming?" checks), `dotnet/EntityFramework.Docs` (branch `live`), `npgsql/doc` and `npgsql/efcore.pg` (source), `JasperFx/marten` `docs/`. Package versions come from `api.nuget.org`. Extension repos (`arkhipov/temporal_tables`, `nearform/temporal_tables`, `xocolatl/periods`) were cloned to read commit dates. `api.pgxn.org` and the GitHub web UI were blocked, so PGXN release dates are not verified.

Short names used below: PG = PostgreSQL docs at `postgres/postgres` `REL_18_STABLE/doc/src/sgml/`; EFD = `dotnet/EntityFramework.Docs/live/entity-framework/core/`; NPD = `npgsql/doc/main/conceptual/`; MTD = `JasperFx/marten/master/docs/`.

---

## 1. Verified facts that constrain the options

### 1.1 PostgreSQL 18

| # | Fact | Source |
|---|---|---|
| P1 | PG 18 added non-overlapping ("temporal") `PRIMARY KEY` / `UNIQUE` via `WITHOUT OVERLAPS` and temporal foreign keys via `PERIOD`, "all applied to the last specified column" (commits fc0438b4e, 89f908a6d). | PG `release-18.sgml` §Constraints (lines ~10700–10730) |
| P2 | `UNIQUE (id, valid_at WITHOUT OVERLAPS)` "behaves like `EXCLUDE USING GIST (id WITH =, valid_at WITH &&)`". The column "must have a range or multirange type. Empty ranges/multiranges are not permitted." Scalar key parts need `btree_gist` ("the expected way to use this feature"). | PG `ref/create_table.sgml`, `UNIQUE` parameter |
| P3 | A `PERIOD` FK is satisfied when referenced rows "completely cover" the referencing row's range; referenced table must have a `WITHOUT OVERLAPS` key. `RESTRICT`, `CASCADE`, `SET NULL` (and `SET DEFAULT`) "In a temporal foreign key, this option is not supported." | PG `ref/create_table.sgml`, `REFERENCES` parameter |
| P4 | **There is no system versioning in PG 18.** No `SYSTEM VERSIONING`, `PERIOD FOR SYSTEM_TIME`, `FOR SYSTEM_TIME AS OF` or `UPDATE ... FOR PORTION OF` anywhere in the 18 doc sources. `FOR PORTION OF` is also absent from `REL_19_STABLE` `ref/update.sgml` (19 is at RC1) and from `master` (now `20devel`). So PG 18 temporal features are *application-time* integrity constraints; the database does not keep old row versions for you. | grep over PG 18 `doc/src/sgml`; `REL_19_STABLE` and `master` `ref/update.sgml`, `ref/create_table.sgml`; `master/configure.ac` |
| P5 | `RETURNING` gained `OLD`/`NEW` in PG 18 for INSERT/UPDATE/DELETE/MERGE (commit 80feb727c). Useful to write a history row from the same statement. | PG `release-18.sgml` |
| P6 | `uuidv7()` (timestamp-ordered UUID) exists in PG 18. | PG `release-18.sgml` (func_uuid_gen_table entries) |
| P7 | Triggers can fire on `TRUNCATE`, "though only `FOR EACH STATEMENT`". Statement triggers can use `REFERENCING OLD TABLE / NEW TABLE` transition relations. | PG `ref/create_trigger.sgml` |
| P8 | Under READ COMMITTED, a second `UPDATE`/`SELECT FOR UPDATE` that hits a row being changed "will wait for the first updating transaction to commit or roll back"; after commit "the search condition of the command (the WHERE clause) is re-evaluated to see if the updated version of the row still matches". | PG `mvcc.sgml` §xact-read-committed |
| P9 | Under REPEATABLE READ the second updater instead fails with `could not serialize access due to concurrent update`. | PG `mvcc.sgml` §xact-repeatable-read |
| P10 | Sequence values are "not reclaimed for re-use if the calling transaction later aborts ... sequence objects cannot be used to obtain 'gapless' sequences". (analysis: values are handed out at `nextval` time, not commit time, so a row with a lower id can commit *after* a row with a higher id.) | PG `func.sgml` §functions-sequence, caution box |
| P11 | `ALTER TABLE ... DISABLE TRIGGER` disables user triggers; "You must own the table to use ALTER TABLE". Firing also depends on `session_replication_role`. So a table owner can switch an append-only trigger off; only a role that does not own the table cannot. | PG `ref/alter_table.sgml` |
| P12 | Privileges `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE` are separately grantable; "the right to modify or destroy an object is inherent in being the object's owner". | PG `ddl.sgml` §ddl-priv |
| P13 | Every table has hidden system column `xmin` (ID of the inserting/last updating transaction). | PG `ddl.sgml` §ddl-system-columns |
| P14 | Contrib modules in 18 include `btree_gist`, `contrib-spi` and others; **no temporal_tables / periods extension ships with PostgreSQL**. | PG `contrib.sgml` |

### 1.2 Extensions for system-versioning on PostgreSQL

| # | Fact | Source |
|---|---|---|
| X1 | `arkhipov/temporal_tables` (C extension, SQL:2011-style system-period tables via a `versioning()` trigger). META.json version 1.2.2. Last commits: 2024-11-04 "Support Postgresql 17", 2026-01-12 Windows CI fix. Linux CI matrix tops out at **PG 17; no PG 18 in CI**. | cloned repo log; `README.md`; `META.json`; `.github/workflows/build-nix.yml` |
| X2 | `nearform/temporal_tables`: a PL/pgSQL rewrite "targeting AWS RDS, Google Cloud SQL, and Azure ... where custom C extensions aren't permitted"; just a SQL script. Last release v1.2.1 on 2025-09-09. PG 18 support not stated (not verified). | cloned repo log; `README.md` |
| X3 | `xocolatl/periods`: "Periods and SYSTEM VERSIONING for PostgreSQL" (C extension). Last commits are Debian packaging: 2025-10-08 "Upload for PostgreSQL 18", functional release 1.2.3 on 2024-09-18. | cloned repo log; `README.md` |
| X4 | The official `postgres` image ships only core + contrib (P14). Any C extension (X1, X3) means a custom Dockerfile layer. (analysis) | P14 |

### 1.3 EF Core 10 and Npgsql

| # | Fact | Source |
|---|---|---|
| E1 | EF Core temporal tables (`ToTable(..., b => b.IsTemporal())`, `TemporalAsOf` etc.) are documented only under the **SQL Server provider**: "SQL Server/Azure SQL temporal tables ... (aka system-versioned tables)". Period columns "always UTC time generated by SQL Server". | EFD `providers/sql-server/temporal-tables.md` |
| E2 | Npgsql has **no** system-versioned/temporal-table feature. It has *temporal constraints* (P1) mapped with `HasKey(...).WithoutOverlaps()` and `PERIOD` FKs, but "Temporal constraints are only supported starting with version 11 of the EF provider, and require PostgreSQL 18." | NPD `EFCore.PG/misc/temporal-constraints.md`; `release-notes/11.0.md`; source `src/EFCore.PG/Extensions/BuilderExtensions/NpgsqlKeyBuilderExtensions.cs` |
| E3 | Npgsql EF provider: latest stable 10.0.3; 11.0 is at `11.0.0-rc.2`. So on our EF Core 10 stack, temporal constraints must be written as raw SQL in a migration. | `api.nuget.org/v3-flatcontainer/npgsql.entityframeworkcore.postgresql/index.json` |
| E4 | `ISaveChangesInterceptor` gives `SavingChanges/SavedChanges/SaveChangesFailed/...` and optimistic-concurrency hooks. The docs' auditing sample warns: "This is not intended to be a robust auditing solution." | EFD `logging-events-diagnostics/interceptors.md` |
| E5 | `ExecuteUpdate`/`ExecuteDelete` "are completely unaware of EF's change tracker, and have no interaction with it whatsoever", run in their own transaction unless one is opened, and "cannot automatically apply concurrency control". (analysis: so a SaveChanges interceptor never sees them; neither does raw SQL or binary COPY.) | EFD `saving/execute-insert-update-delete.md` |
| E6 | On Npgsql, map a `uint` to `xmin` with `[Timestamp]`/`IsRowVersion()` to get an optimistic concurrency token. From provider **11.0**, `40001`/`40P01` during `SaveChanges` become `DbUpdateConcurrencyException`; before that they surface as `DbUpdateException`. | NPD `EFCore.PG/modeling/concurrency.md`; `release-notes/11.0.md` |
| E7 | Npgsql binary COPY (`BeginBinaryImport`) is the fast bulk-load path. | NPD `Npgsql/copy.md` |

### 1.4 Marten

| # | Fact | Source |
|---|---|---|
| M1 | Marten 9.54.2 (latest stable) targets net10.0/net9.0; depends on Weasel.Postgresql 9.47.0. Supports **PostgreSQL 13 or later**; its own dev compose uses `postgres:17`. | `marten.nuspec` 9.54.2; repo `README.md` |
| M2 | Events live in `mt_events` (`seq_id` sequential PK, `stream_id`, `version`, `data` JSONB, `type`, `timestamp`, ...), plus `mt_streams`, `mt_event_progression`. | MTD `events/storage.md` |
| M3 | Projections: Inline (same unit of work as the append), Async (background daemon, eventual consistency), Live (in-memory on demand). | MTD `events/projections/index.md`, `inline.md` |
| M4 | Optimistic concurrency per stream: `FetchForWriting` (strongly recommended), `AppendOptimistic`, `AppendExclusive`, expected-version appends. | MTD `events/appending.md` |
| M5 | Async daemon tracks a **high water mark** that "will frequently be a little behind the highest known event sequence number if outstanding gaps in the event sequence are detected" (same out-of-order-commit problem as P10). | MTD `events/projections/async-daemon.md` |
| M6 | `Marten.EntityFrameworkCore` 9.54.2 projects events into EF Core `DbContext` entities (Inline/Async/Live), depends on `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0 for net10.0; tables are migrated by Weasel, "You do not need to run `dotnet ef database update` separately". | MTD `events/projections/efcore.md`; `marten.entityframeworkcore.nuspec` |

---

## 2. Pieces every option shares

Whatever pattern is chosen, these tables exist and are append-only by nature (an import or a decision is a fact that happened):

```
member_state(code PK, name, ...)
import(id PK, file_name, file_sha256, file_blob_or_path, uploaded_by, uploaded_at,
       status, report_json)                       -- HIS-01
import_row(import_id FK, line_no, raw_text, outcome, reason, vote_code, state_code)
                                                  -- HIS-01 "outcome of every row"
snapshot(id PK, name, created_by, created_at, published_at NULL, published_by NULL, ...)
snapshot_result(snapshot_id FK, state_code, index_value numeric, numerator, denominator)
                                                  -- the published numbers themselves
```

**(analysis)** SNP-04 has two halves. "Shows exactly the same numbers" is met by *storing the computed results* (`snapshot_result`); never recompute on open. "Exactly which data was it based on" needs *input provenance*; that is where the options differ (section 4).

At ~1M country votes, **the import path must not go through per-entity EF change tracking** (E7; (analysis) 1M tracked entities is minutes and gigabytes). Bulk-load the file into `import_row` with binary COPY, then classify with set-based SQL (`INSERT ... SELECT`). This matters for audit design: any audit mechanism that relies on the change tracker (E4, E5) is *bypassed* by the only import path that performs well.

---

## 3. The options

### Option A. Current-state tables plus a generic JSON audit log

**Schema sketch**

```
vote(id PK, code UNIQUE, session, date, title, position NULL, weight NULL, xmin)
country_vote(vote_id, state_code, choice CHAR(1), source_import_id, PK(vote_id,state_code), xmin)
conflict(id PK, vote_id, state_code, incoming_choice, incoming_import_id,
         status, decided_by, decided_at, comment, xmin)
audit_log(id bigserial PK, entity, entity_key, action, before JSONB, after JSONB,
          actor, at, import_id NULL, conflict_id NULL)
```

Written either by an EF `SaveChangesInterceptor` that walks `ChangeTracker.Entries()` (E4), or by explicit service code.

**Scenario**
1. File 1 (Dana): COPY into `import_row`; set-based insert into `country_vote` (BRA, Y, import 1). Audit: either 1M `audit_log` inserts written by hand in the SQL (the interceptor does not see it, E5), or one coarse "import 1 inserted N rows" entry.
2. Snapshot 2024-Q1 computed from `country_vote`, results stored. Inputs: must be copied (nothing else is stable; `country_vote` will be overwritten).
3. rev2: classification sees A ≠ Y, inserts `conflict` (incoming A, import 2). `country_vote` untouched (ING-08).
4. Dana accepts: `UPDATE country_vote SET choice='A', source_import_id=2` + `UPDATE conflict SET status='Accepted'...`; interceptor writes `audit_log(before {"choice":"Y","src":1}, after {"choice":"A","src":2})`.
5. Omar rejects concurrently: `xmin` concurrency token on `conflict` (E6) makes his `SaveChanges` throw `DbUpdateConcurrencyException`; UI shows "already decided by Dana".
6. Timeline: `SELECT ... FROM audit_log WHERE entity_key LIKE 'MA/2024/101%'` parse JSON, plus imports and conflicts.

**Requirements**
- ING-08: by code discipline only (the current row *is* overwritten on accept; nothing structural stops an import overwriting it).
- HIS-01: yes (shared tables).
- HIS-02: weak. "Where it came from" is a column on the current row; previous values live only inside JSON blobs, and only if every write path remembered to log. Bulk SQL bypasses the interceptor (E5).
- HIS-03: yes if the interceptor/service logs; data is untyped JSON.
- HIS-04: yes, but the timeline is assembled by parsing JSON with ad-hoc shapes.
- CON-02: approved value's source is `country_vote.source_import_id`; incoming source on `conflict`. OK.
- CON-05: OK via `xmin` token (E6) or conditional update (section 5).
- SNP-04: only by copying all input values into the snapshot (section 4). Referencing ids is impossible because the referenced rows mutate.

**Query cost at 1M**: current reads are the cheapest of all options (one row per fact). Audit log grows by one JSON row per change; small after the initial load unless the initial load is also logged row by row (1M JSONB rows, ~200–300 MB, analysis).

**Effort (3 days)**: lowest to get running (~0.5 day). Hidden cost: making bulk paths audit correctly.

**Testability**: easy to unit test the interceptor with Testcontainers; hard to *prove* completeness (any new write path can forget).

**DECISIONS.md framing**: "Current tables are the truth; a generic audit trail records changes." Simple to explain, but the brief's language ("no value overwritten without a trace", "exactly which data") will make a reviewer ask "what about writes the interceptor doesn't see?" Microsoft's own sample disclaims being robust (E4).

**Reviewer view (analysis)**: a familiar CRUD-plus-audit pattern; reads as the minimum. Untyped JSON history for the core domain fact looks like history was bolted on, which is the thing this brief is testing.

---

### Option B. Typed append-only revision tables plus a current pointer

**Schema sketch**

```
vote(id PK, code UNIQUE)                                         -- identity only
vote_details_revision(id PK, vote_id FK, session, date, title,
                      source_kind, source_import_id NULL, source_conflict_id NULL,
                      created_by, created_at, supersedes_id NULL FK self)
vote_position_revision(id PK, vote_id FK, position, weight, changed_by, changed_at,
                       comment, supersedes_id NULL)                  -- HIS-03
country_vote_revision(id bigint PK, vote_id, state_code, choice,
                      source_kind ('import'|'conflict_accept'), source_import_id,
                      source_import_row, decided_conflict_id NULL,
                      created_by, created_at, supersedes_id NULL)
-- current pointers (the only mutable rows besides conflict.status)
country_vote_current(vote_id, state_code, revision_id FK, PK(vote_id,state_code))
vote_current(vote_id PK, details_revision_id FK, position_revision_id FK NULL)
conflict(id PK, vote_id, state_code, approved_revision_id FK, incoming_import_id,
         incoming_import_row, incoming_choice, status, decided_by, decided_at,
         comment, resulting_revision_id NULL FK)
snapshot_vote(snapshot_id, vote_id, details_revision_id, position_revision_id,
              position, weight)                   -- copied + referenced
snapshot_input(snapshot_id, country_vote_revision_id)  -- see section 4
```

Variant B': drop the pointer table and mark current with `is_current` plus a partial unique index, or derive "latest" with `DISTINCT ON`. The pointer table keeps the revision tables strictly insert-only, which makes DB enforcement (section 6) trivial.

**Scenario**
1. File 1: COPY into `import_row`; `INSERT INTO country_vote_revision ... SELECT` (rev #r1: BRA, Y, import 1, row n, Dana); `INSERT INTO country_vote_current` (pointer → r1).
2. Snapshot 2024-Q1: in one REPEATABLE READ transaction read the pointers for the chosen votes, compute, store `snapshot_result`, `snapshot_vote` and `snapshot_input(→ r1)`.
3. rev2: classification joins incoming rows to `country_vote_current`/`country_vote_revision`; A ≠ Y → `conflict(approved_revision_id=r1, incoming A, import 2 row m)`. No revision row, no pointer change (ING-08).
4. Dana accepts with comment: in one transaction, `UPDATE conflict SET status='Accepted', ... WHERE id=@id AND status='Open'` (1 row) → `INSERT country_vote_revision r2 (A, source conflict, supersedes r1)` → `UPDATE country_vote_current SET revision_id=r2 WHERE ... AND revision_id=r1`.
5. Omar's reject: his conditional update matches 0 rows after Dana commits (P8) → 409 "Dana accepted this conflict at 10:42".
6. Published snapshot still references r1, which is immutable → still Y. New snapshot picks up pointer → r2 → A.
7. Timeline: `UNION ALL` of typed tables for the vote: details revisions, position revisions, country vote revisions, conflicts (opened/decided), import rows that touched the vote, snapshots that included it.

**Requirements**
- ING-08: structural. Imports only ever insert revisions for keys with no current pointer; changing a pointer happens only in the conflict-accept command. Can be enforced in the DB (section 6).
- HIS-01: shared tables; every revision links to its `import_row`.
- HIS-02: strongest of the relational options: each revision has typed provenance and `supersedes_id`; nothing is updated except the pointer, whose history is the revision chain itself.
- HIS-03: `conflict` row (who/when/comment, approved revision = previous, resulting revision = new) and `vote_position_revision` (previous = superseded row).
- HIS-04: typed `UNION ALL`, indexable, no JSON parsing.
- CON-02: `conflict.approved_revision_id → revision → import → uploaded_by/at` on one side; `incoming_import_id` on the other. A direct join.
- CON-05: conditional update on `conflict` (section 5), optionally also on the pointer.
- SNP-04: reference immutable revision ids, or a watermark (section 4). Copying is unnecessary.

**Query cost at 1M**: initial load = 1M revisions + 1M pointers (two insert-selects; (analysis) seconds with COPY + set-based SQL). Current reads are a PK join pointer→revision; with ~150 states per vote and a quarter of ~40 votes a snapshot touches ~6k rows; even an all-history snapshot (1M) is one hash join, sub-second to a few seconds (analysis, not measured). Revision table grows only by corrections (rare).

**Effort (3 days)**: about 1–1.5 days for the data layer. EF Core maps it as ordinary entities (no special features); imports use COPY + raw SQL; commands are small transactional services.

**Testability**: high. Each rule is a SQL-level invariant: "import never changes a pointer", "accept inserts exactly one revision", "two concurrent decisions → one 409". All testable with Testcontainers PostgreSQL 18 and two parallel connections.

**DECISIONS.md framing**: "Approved values are immutable revisions; 'current' is a pointer; decisions move the pointer and are themselves records. Snapshots reference revisions, so nothing they used can change." One paragraph plus a diagram.

**Reviewer view (analysis)**: reads as deliberate domain modelling: history is first-class, typed and queryable, without a framework. Likely questions: pointer vs `is_current`, and how snapshot provenance scales; both have direct answers.

---

### Option C. Full event sourcing with projections

**Shape (Marten)**

```
mt_events(seq_id, stream_id, version, type, data JSONB, timestamp)     (M2)
mt_streams(id, version, ...)
streams: one per vote (VoteImported, VoteDetailsChanged, PositionSet, CountryVoteRecorded,
         ConflictOpened, ConflictAccepted, ConflictRejected) -- or one per conflict
projections (Inline, M3; optionally to EF tables via Marten.EntityFrameworkCore, M6):
  current_country_vote(vote, state, choice, last_event_seq)
  vote_timeline(...), open_conflicts(...)
snapshot: stores results + the event seq_ids / high-water mark used
```

Hand-rolled alternative: one `event(seq, stream_id, version, type, payload jsonb, actor, at)` table with `UNIQUE(stream_id, version)` and projection code in the same transaction.

**Scenario**
1. File 1: append `CountryVoteRecorded{BRA,Y,import 1}` events. Either 1M events, or one `VoteImported` event per vote carrying ~150 choices (~7k events, analysis). Inline projection writes `current_country_vote`.
2. Snapshot: read projection, store results and the seq ids / max seq it reflected.
3. rev2: `ConflictOpened` appended to the vote stream.
4. Dana: `FetchForWriting(vote stream)` → check conflict open → append `ConflictAccepted{A, comment}`; inline projection updates current.
5. Omar: his append with the stale expected version fails (M4) → message.
6. Published snapshot unchanged (results stored, events immutable). Timeline = the stream, rendered.

**Requirements**
- ING-08, HIS-02/03/04: natural; the event log is the history.
- HIS-01: still needs the import/import_row tables (events don't hold "outcome of every row" cheaply).
- CON-02: source of the approved value must be carried in event data or looked up from the event that produced the current value.
- CON-05: stream optimistic concurrency (M4).
- SNP-04: reference event seq ids. A "max seq_id" watermark suffers the gap problem (M5, P10) unless the snapshot reads under the daemon's high water mark logic.

**Query cost at 1M**: the snapshot must read a *projection*, never replay 1M events per request. With Inline projections the cost equals Option B's reads plus JSONB overhead on writes. Async projections add eventual consistency: a snapshot created right after an accept might miss it unless it waits for the daemon (M3, M5). Rebuilding a projection = replaying every event.

**Effort (3 days)**: high. Learning Marten's projection lifecycles, Weasel migrations alongside EF migrations (M6), event versioning, and a second persistence model. Hand-rolled is worse: you write the projection engine. (analysis) Realistically 2+ days of the 3.

**Testability**: aggregate logic is very testable (given events, when command, then events); end-to-end projections and daemon timing are harder.

**DECISIONS.md framing**: "The vote stream is the record; tables are projections." Powerful, but needs explaining why ES fits a CSV-reconciliation tool, and why EF Core is mostly sidelined when the brief names EF Core 10.

**Reviewer view (analysis)**: split. Some will see ambition; many consulting reviewers will see over-engineering for a 3-day task and risk to delivery. Also the brief explicitly lists EF Core + Npgsql; centring Marten moves away from the stack they asked to see.

---

### Option D. PostgreSQL-native history

**D1. Trigger-maintained history tables**

```
country_vote(... current row ..., source_import_id, changed_by, changed_at)
country_vote_history(LIKE country_vote, history_id bigserial PK, valid tstzrange,
                     op char(1), txid)
trigger AFTER UPDATE OR DELETE ON country_vote FOR EACH ROW
  INSERT INTO country_vote_history SELECT OLD.*, tstzrange(OLD.changed_at, now()), TG_OP...
```

- Who did it: the trigger cannot see the app user; pass it via `SET LOCAL app.user = 'dana'` and `current_setting('app.user')` in the trigger (analysis; standard GUC usage), or require `changed_by` columns that the app sets.
- Scenario: accept = `UPDATE country_vote SET choice='A'`; trigger copies the Y row to history. Snapshot reproducibility: copy values, or reference `history_id`s, which only exist *after* a change, so references must point to "current row as of txid X" (awkward). Timeline = history ∪ current.
- Strength: catches *every* write path, including raw SQL, `ExecuteUpdate` and bulk statements (E5 gap closed). PG 18 `RETURNING OLD` (P5) is an alternative to triggers for explicit writes.
- Weakness: history is a mechanical row diff ("row changed"), not a domain record ("conflict 17 accepted by Dana with comment"). You still need the conflict table and typed decisions for HIS-03/CON-02. Trigger logic lives outside EF migrations' model (raw SQL in migrations).

**D2. PG 18 temporal constraints (application-time)**

```
country_vote_version(vote_id, state_code, choice, valid tstzrange, source...,
  PRIMARY KEY (vote_id, state_code, valid WITHOUT OVERLAPS))     -- needs btree_gist (P2)
```

- Guarantees no two versions of one fact overlap in time (P1, P2). Closing the old version and opening the new is still two statements the app writes (`UPDATE ... SET valid = tstzrange(lower(valid), now())` then `INSERT`); there is no `FOR PORTION OF` in 18 (P4).
- Note: closing the range *updates* the old row, so the history row is not strictly append-only. Temporal FKs cannot `CASCADE`/`RESTRICT` (P3).
- EF Core 10 + Npgsql 10 cannot model it (E2, E3): raw SQL migration, and EF sees a composite key containing an `NpgsqlRange<DateTimeOffset>`.
- Fits as a *constraint layered on Option B* (one current version per fact, enforced), not as a history mechanism on its own. "As of time T" queries become `valid @> T`, which is natural for snapshots taken at a timestamp but has the same commit-order caveat as watermarks (section 4).

**D3. System-versioning extensions** (temporal_tables, periods)

- PG 18 has no built-in system versioning (P4). `arkhipov/temporal_tables` CI stops at PG 17 (X1); `periods` has a Debian PG 18 packaging upload but its last functional release is 2024 (X3); `nearform/temporal_tables` is plain PL/pgSQL (X2), so it is just D1 with someone else's trigger.
- Needs a custom Docker image for C extensions (X4) and an argument about maintenance risk. Same "mechanical diff, not domain record" limitation as D1.

**D overall**
- ING-08: not addressed by history capture; needs the same structural rule as B.
- HIS-02: D1/D3 strong for "no change without trace" (catches everything); provenance only as good as the columns.
- HIS-03/04, CON-02: need typed domain tables anyway.
- CON-05: same as A/B.
- SNP-04: copy values or reference history ids + "current as of".
- Cost at 1M: triggers add one insert per changed row; negligible here (corrections are rare). Initial load: no history rows (inserts not versioned).
- Effort: D1 ~0.5 day of SQL on top of A; D2 0.5 day on top of B; D3 adds Docker and risk.
- Reviewer view (analysis): D1 as *defence in depth* under A looks competent; as the sole history model it looks like DBA plumbing that skips the domain. D3 on unmaintained extensions is a red flag. D2 shows PG 18 awareness; worth a sentence, not the core design.

---

### Option E. EF Core built-in features

- **Temporal tables**: SQL Server only (E1). Nothing equivalent on Npgsql (E2). Not an option on this stack; say so in DECISIONS.md in one line, since a reviewer from a SQL Server shop may ask.
- **SaveChanges interceptor auditing** (E4): this *is* Option A's writer. Fits Npgsql unchanged. Blind spots: `ExecuteUpdate`/`ExecuteDelete`, raw SQL and COPY (E5), i.e. exactly the bulk import path.
- **Temporal constraints via Npgsql**: provider 11 only (E2, E3), which is RC and pairs with EF Core 11 / .NET 11; on our EF Core 10 stack use raw SQL in migrations.
- **Concurrency tokens**: `xmin` mapping (E6) works on any option for CON-05. On provider 10 serialization failures surface as `DbUpdateException`, not `DbUpdateConcurrencyException` (E6), so catch both or use the conditional-update pattern.
- **Reviewer view (analysis)**: knowing that EF temporal tables are SQL Server only, and that interceptors don't see bulk operations, is itself a good signal to put in DECISIONS.md.

---

## 4. How a snapshot records what it used (SNP-04)

| Method | What is stored | Size (analysis, not measured) | Exact? | Works with |
|---|---|---|---|---|
| Store results | `snapshot_result` per state (~150 rows) | tiny | Exact numbers; says nothing about inputs | all (always do this) |
| Copy inputs | `(snapshot_id, vote_id, state, choice)` + per-vote position/weight/details | ~40–50 B/row heap + PK index; a quarter (~6k rows) is trivial; an all-history snapshot (1M) ≈ 70–90 MB each | Exact, self-contained, but duplicates data and loses the *link* to sources unless the source import id is copied too | A, D (only choice) |
| Reference revision ids | `(snapshot_id, country_vote_revision_id)` + `snapshot_vote(details_rev, position_rev)` | ~same row count as copy but narrower; FK-backed | Exact, and answers "which data, from which file, approved by whom" by join | B, C (event seq ids), D2 |
| Watermark | one `max(revision_id)` or a timestamp; reproduce with "latest revision ≤ watermark" | 8 bytes | **Not safe by itself**: ids/timestamps are assigned before commit, so a revision with a lower id can commit after the snapshot read it (P10; Marten's own high-water-mark gap handling M5). Safe only if approval writes and snapshot capture are serialized (e.g. a lock) | B, C, D2 |

**(analysis)** Snapshots are "over a chosen set of votes", usually a quarter, so explicit references are a few thousand rows; the watermark optimisation is not worth its correctness argument. Positions and weights should be *copied* into `snapshot_vote` as well as referenced, because they are what an auditor reads first.

## 5. CON-05 patterns (any option)

1. **Conditional update (recommended)**: `UPDATE conflict SET status=@s, decided_by=@u, decided_at=now(), comment=@c WHERE id=@id AND status='Open'`. Under READ COMMITTED the second transaction waits and then re-evaluates the WHERE on the committed row (P8): 0 rows → return 409 with who decided. Doable with `ExecuteUpdateAsync` (returns affected rows; no concurrency token needed, E5) inside an explicit transaction together with the revision insert.
2. **Optimistic token**: map `xmin` (E6) on `conflict`; the loser gets `DbUpdateConcurrencyException` from `SaveChanges`.
3. **Pessimistic**: `SELECT ... FOR UPDATE` then check status (P8).
4. **Belt and braces**: `UNIQUE (conflict_id)` on a `conflict_decision` table if decisions are a separate insert-only table, so a second decision cannot exist even if code is wrong.

Test: two connections, barrier, both decide; assert exactly one decision row, one new revision, one 409.

## 6. Cross-cutting: enforcing append-only in the database

Mechanisms:
- `BEFORE UPDATE OR DELETE ... FOR EACH ROW` trigger that raises an exception, plus a `BEFORE TRUNCATE ... FOR EACH STATEMENT` trigger (P7), on `*_revision`, `import`, `import_row`, `snapshot_result`, `snapshot_input`, decision tables. For `snapshot`, allow only the one-time `published_at` transition (or keep publication in its own insert-only table).
- Privileges: run the app as a role that has `SELECT, INSERT` only on those tables (P12) and does **not own** them, so it cannot `ALTER TABLE ... DISABLE TRIGGER` (P11). Migrations run as the owner role.

Pros
- Turns ING-08/HIS-02/SNP-04 from "we promise" into "the database refuses", which is easy to demonstrate in a test (`UPDATE country_vote_revision ...` → error).
- Catches bugs in bulk SQL that EF never sees (E5).
- Cheap: ~30 lines of SQL in a migration.

Cons
- Owner/superuser can still bypass (P11, P12); it is a guard against mistakes, not against a DBA. Say so honestly.
- GDPR-style erasure or fixing a bad import would need an explicit, owner-run procedure.
- Tests that clean tables between runs must use the owner role or recreate the database (e.g. a fresh Testcontainers database per test class).
- With Option A it can only protect the audit log, not the current tables (which must update).

**(analysis)** Highest value-to-effort item in this whole document for a reviewer reading "never overwritten, by any process".

---

## 7. Comparison

Ratings: ++ strong, + adequate, ~ weak/conditional, − poor.

| | A. Current + JSON audit | B. Typed revisions + pointer | C. Event sourcing (Marten) | D1. Triggers → history | D2. PG18 temporal keys | E. EF temporal tables |
|---|---|---|---|---|---|---|
| ING-08 | ~ code discipline | ++ structural, DB-enforceable | ++ | ~ (capture only) | + (with B) | n/a |
| HIS-01 | + shared | + shared | + shared | + shared | + shared | n/a |
| HIS-02 provenance + prior values | ~ JSON, bypassable | ++ typed | ++ | + all writes, untyped intent | + | n/a |
| HIS-03 decisions/weights | + | ++ | ++ | ~ needs domain tables | + | n/a |
| HIS-04 timeline | ~ parse JSON | ++ typed UNION | ++ stream | ~ | + | n/a |
| CON-02 both sources | + | ++ by FK | + | + | + | n/a |
| CON-05 | + | + | ++ built-in | + | + | n/a |
| SNP-04 provenance | ~ copy only | ++ reference ids | + seq ids | ~ copy | + as-of ranges | n/a |
| Read cost @1M | ++ | + one join | + if inline projection | ++ | + GiST | n/a |
| 3-day effort | ++ ~0.5 d | + ~1–1.5 d | − 2+ d, new stack | + +0.5 d | ~ raw SQL, EF 10 can't map | − not available on Npgsql |
| Testability | + (completeness hard) | ++ | + | + | + | n/a |
| Explaining in DECISIONS.md | + | ++ | ~ justify ES | + | + | one line: SQL Server only |
| Reviewer impression (analysis) | baseline | deliberate, proportionate | ambitious / risky | good as defence-in-depth | nice PG18 touch | — |

## 8. Recommendation (for the user to decide in ticket 15)

**Recommendation, not a decision:** Option B, typed append-only revision tables with a current pointer, plus:

1. **DB-enforced append-only** (section 6) on revision, import, decision and snapshot tables, with a non-owner app role. This is the visible proof of ING-08 and "no trace lost".
2. **Snapshots store results, copy position/weight per vote, and reference the exact country-vote and details revision ids** (section 4). No watermark shortcut.
3. **CON-05 by conditional update** on `conflict` in the same transaction as the revision insert and pointer move (section 5), with a concurrent test.
4. **Imports via COPY + set-based SQL**, inserting revisions directly; no interceptor-based auditing (it would miss them, E5).
5. Optional, if time remains: a PG 18 `WITHOUT OVERLAPS` key or a partial unique index to enforce "one current value per fact" (D2), via raw SQL migration since EF Core 10/Npgsql 10 cannot model it (E2, E3).
6. In DECISIONS.md, one paragraph on rejected options: EF temporal tables are SQL Server only (E1); PG 18 has no system versioning (P4); the temporal_tables extension's CI stops at PG 17 (X1); generic JSON audit misses bulk writes (E5); event sourcing was judged disproportionate for 3 days and would sideline the requested EF Core stack.

Reasoning: B is the only option where every requirement in the list is met *by the shape of the data* rather than by discipline, it uses EF Core as plain mapping with no exotic features, its cost at 1M rows is one indexed join, and the scenario (Y stays in the published snapshot because it references immutable revision r1; A appears in the next one via the pointer) can be shown in a single integration test. The main risk is the extra join and the pointer table in every read; that is mitigated by a view (`country_vote_current_v`) that the scoring and UI queries use.

## 9. Not verified

- PGXN release dates for temporal_tables and periods (api.pgxn.org blocked); PG 18 compatibility of `nearform/temporal_tables`.
- Marten's tested PostgreSQL 18 support (README says 13+; CI image is 17).
- All sizes and timings in sections 3–4 are estimates, not measurements; ticket 13 (historic file) should measure the 1M-row load and a full-history snapshot.
- learn.microsoft.com and postgresql.org rendered pages were not reachable; content was read from their source repositories, which may differ trivially from the published pages.
