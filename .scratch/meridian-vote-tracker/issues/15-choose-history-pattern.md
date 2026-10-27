# Choose the history storage pattern

Type: grilling
Status: resolved
Blocked by: 14

## Question

Using [Compare history storage patterns](14-compare-history-patterns.md), choose how history is stored, together with the user. The chosen design must satisfy what [Design the data model and history](04-data-model-and-history.md) decides must be recorded. Revisit there whether history tables should refuse UPDATE and DELETE at the database level (deferred from that ticket). Record the decision and its trade-offs for DECISIONS.md, and how AI helped compare the options for AI_USAGE.md.

## Answer

Chosen with the user from the six options in [Compare history storage patterns](14-compare-history-patterns.md); drawn in the [data model sketch](../research/15-data-model-sketch.md) (shape only; final columns and indexes go to [Lock the database schema](16-lock-database-schema.md)).

1. **Pattern B: typed append-only revisions.** Country votes, vote details and positions each keep an insert-only revision history; every revision carries its origin. Rejected: (A) current tables plus a JSON audit log, whose interceptor misses bulk SQL and whose history is untyped; (C) event sourcing, too much for 3 days and it sidelines the requested EF Core; (D1) trigger copies, which record row diffs rather than decisions; (E) EF temporal tables, SQL Server only, with no system versioning in PostgreSQL 18.
2. **"Current" is a separate pointer table** (one for vote details and position, one for country votes): the approved value right now, which a new snapshot would use. It is the only data that moves; history rows are never updated.
3. **Database guard**: a trigger refuses UPDATE and DELETE on import, import row, the three revision tables, conflict, conflict decision, not-applied attempts and calculated snapshots, with a test proving it. It guards against mistakes, not a database owner; running the app as a non-owner role is listed as a before-production item.
4. **Same pattern for vote details and positions.**
5. **A decision is its own insert-only record, at most one per conflict** (unique). A conflict is pending while no decision exists. The database rejects a second decision; the loser gets 409 and a not-applied record. This replaces the conditional status update from [Decide concurrency and roles](06-concurrency-and-roles.md) with an equivalent guarantee. The per-vote lock stays.
