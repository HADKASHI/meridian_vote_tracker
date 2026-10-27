# Choose how data is read and written

Type: grilling
Status: resolved
Blocked by: 02

## Question

Which data-access approach, and why (section 9 asks us to explain it)? Likely EF Core with migrations for the model and constraints, plus raw SQL or PostgreSQL COPY for the bulk paths (a ~1M-row historic import, snapshot computation over all history). Decide where each is used and how migrations run under docker compose.

## Answer

Decided with the user in two grilling rounds. The user overturned the first proposal for snapshot calculation (Q6).

1. **EF Core is the one data-access library** (Npgsql provider). Hand-written SQL goes through EF's own `Database.SqlQuery<T>` / `ExecuteSql`. No Dapper.
2. **Code-first migrations hold the whole schema.** Whatever EF can't model is raw SQL inside the same migrations (`migrationBuilder.Sql`): the UPDATE/DELETE guard trigger, the views, partial unique indexes. Names are snake_case via `EFCore.NamingConventions` (10.0.1), so hand-written SQL needs no quoted identifiers.
3. **Migrations run as a one-shot `migrate` service in docker compose**, using an EF migration bundle. `api` (and any worker) start only after it exits successfully (`depends_on: condition: service_completed_successfully`). Rejected: `MigrateAsync()` at app startup, which races once there is more than one app process; a `psql`-run SQL script.
4. **The import's write path sits behind one seam, `ImportWriter`**, which persists one vote's result per transaction: row records, revisions, conflicts, pointer moves. Classification stays pure logic over parsed rows plus approved values; the writer only persists. It is built first with EF (`AddRange` + `SaveChanges` per vote, change tracker cleared; generated ids come back through `RETURNING`). **Budget**: the historic ~1M-row import finishes in under 5 minutes in compose on a laptop, measured on a synthetic file. Only if it misses the budget do the internals switch to Npgsql binary COPY, with ids reserved from sequences first, without touching classification.
5. **Screens read through EF `AsNoTracking()` projections** straight into response DTOs; entities never reach the API. **"Current value" is a PostgreSQL view** (pointer joined to its revision), mapped as a keyless entity and used by classification, snapshots and screens alike.
6. **Snapshots: the database supplies facts, C# applies rules.** Inside one `REPEATABLE READ` transaction:
   - a query streams facts from the current views, one line per (state, vote): ballot or none, member on the vote date, stance, weight;
   - a pure, unit-tested C# scoring function turns them into counted and matched weights, the labelled breakdown, n/a and ranks;
   - the snapshot's inputs (exact revision ids) and its results are written.

   Rejected: one set-based SQL statement. It would be faster, but it would hide the scoring rules inside SQL, out of reach of unit tests and of DECISIONS.md. Even across all of history the facts are tens of MB.
7. **The per-vote lock is `SELECT … FROM vote WHERE id = @id FOR UPDATE`**, taken first by both an import transaction and a conflict decision. It doesn't fire the guard trigger, ends with the transaction, and needs no lock-key scheme. Only the serial import worker creates votes, so there's no race to create one twice.
8. **No generic repository.** Write use cases get named modules that own their transaction, lock and SQL: `ImportWriter`, conflict decisions, position changes, snapshot creation. They are tested against real PostgreSQL, because EF's in-memory provider runs no trigger, lock or unique constraint. Reads are plain EF projections. Pure logic (classification, scoring) is unit-tested with no database. Rejected: (a) `DbContext` used freely in handlers, which spreads the race-sensitive steps around; (b) `IRepository<T>`, which duplicates EF and splits the work by table rather than by use case. The use-case modules also give the four-developer split in DECISIONS.md natural seams.
