# Compare history storage patterns

Type: research
Status: claimed

## Question

Lay out three to five concrete ways to store history in PostgreSQL for this system, with the trade-offs of each, so the user can choose in [Choose the history storage pattern](15-choose-history-pattern.md). Candidates at least: a current table plus a generic audit log; typed append-only revision tables with a current pointer; full event sourcing with projections; PostgreSQL-native options (triggers writing history tables, temporal tables or PostgreSQL 18 temporal constraints); and EF Core features such as temporal tables, which may be SQL Server only. Judge each against HIS-01 to HIS-04, ING-08, CON-02, CON-05 and SNP-04, the snapshot question "which data was the published index based on", query cost at ~1M country votes, effort in a 3-day build, and how easy it is to explain in DECISIONS.md. Show how each handles the running scenario: Brazil Y in vote 101, a later file says A, Dana accepts. Facts about PostgreSQL and EF Core must cite primary sources.
