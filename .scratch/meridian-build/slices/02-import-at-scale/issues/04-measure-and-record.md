# 04: Measure and record in ADR 5

**What to build:** Run the `Performance` test, write the measured time and peak memory into ADR 5 (data access), and confirm the slice 1 tests still pass. If the 5-minute budget or about 1 GB of peak memory is missed, stop and add a fix ticket (COPY inside `ImportWriter`, or a two-pass parser) before closing this one; classification stays untouched.

**Blocked by:** 02 Performance test, 05 Write a vote's rows with COPY (if the budget was missed).

**Status:** done

- [x] The measured time and peak memory, and where they were measured, are in ADR 5
- [x] The budget (5 minutes, about 1 GB) is met, or a fix ticket was added and done first, or the miss is recorded with the user's decision (changed 2026-10-29: the miss is accepted, see below)
- [x] All slice 1 tests still pass

## Comments

- Budget missed with EF (06:50, 1,471 MiB growth). COPY (ticket 05) met it (03:02, 732 MiB growth) but was not adopted: the user decided to keep the EF writer and the existing 200 MB upload limit, so the 5-minute budget for the historic-sized file stays unmet. ADR 5 records the numbers and what a file near the 200 MB cap needs (measured: about 16 minutes and a 2.8 GiB worker peak for a 190 MB file). Measured in a shared-CPU cloud container with PostgreSQL in Docker, not on a laptop.
- `DECISIONS.md` lines 44 and 58 still describe the writer as EF-only; slice 8 rewrites that file.
- 2026-10-29: the user said the 5-minute budget is not a hard limit and a 3 GB worker is fine, provided it is documented and the worker really has that memory. Measured a 190 MB file in the real worker container: 15:53 and a 2.80 GiB peak. `compose.yaml` now gives the worker `mem_limit: 5g` (3.75 GiB .NET heap), because the largest accepted file projects to about 3.1 GiB; ADR 5 records the numbers and the reasoning.
