# Compare history storage patterns

Type: research
Status: resolved

## Question

Lay out three to five concrete ways to store history in PostgreSQL for this system, with the trade-offs of each, so the user can choose in [Choose the history storage pattern](15-choose-history-pattern.md). Candidates at least: a current table plus a generic audit log; typed append-only revision tables with a current pointer; full event sourcing with projections; PostgreSQL-native options (triggers writing history tables, temporal tables or PostgreSQL 18 temporal constraints); and EF Core features such as temporal tables, which may be SQL Server only. Judge each against HIS-01 to HIS-04, ING-08, CON-02, CON-05 and SNP-04, the snapshot question "which data was the published index based on", query cost at ~1M country votes, effort in a 3-day build, and how easy it is to explain in DECISIONS.md. Show how each handles the running scenario: Brazil Y in vote 101, a later file says A, Dana accepts. Facts about PostgreSQL and EF Core must cite primary sources.

## Answer

Comparison with sources: [research/14-compare-history-patterns.md](../research/14-compare-history-patterns.md). It walks the Brazil scenario through six options: (A) current tables plus a JSON audit log, (B) typed append-only revisions with a current pointer, (C) event sourcing with Marten, (D1) triggers copying into history tables, (D2) PostgreSQL 18 temporal keys, and (E) EF Core features.

Facts the choice rests on:
- PostgreSQL 18 has no system-versioned tables. It only added `WITHOUT OVERLAPS` keys and `PERIOD` foreign keys (needs btree_gist; no `FOR PORTION OF`).
- EF Core temporal tables are SQL Server only. Npgsql supports PostgreSQL temporal constraints only from provider 11, still RC, so on 10.0.x they would be raw SQL in a migration.
- A SaveChanges interceptor cannot see `ExecuteUpdate`, raw SQL or COPY, which leaves a gap in option A on the bulk import path.
- The temporal_tables and periods extensions are unmaintained or unverified on PostgreSQL 18 and not in the official image.
- Marten 9.x supports .NET 10, but it means a second persistence model plus a projection daemon.
- A "max revision id" or timestamp watermark is unsafe for snapshots, because sequence ids commit out of order. Snapshots should reference exact revision ids.
- CON-05 works as a conditional `UPDATE ... WHERE status = 'open'` under READ COMMITTED; an `xmin` concurrency token is the alternative.
- Database-level append-only guards (triggers plus an insert-only app role) protect against mistakes, not against a table owner.

The researcher recommends B. The choice is the user's, in [Choose the history storage pattern](15-choose-history-pattern.md).
