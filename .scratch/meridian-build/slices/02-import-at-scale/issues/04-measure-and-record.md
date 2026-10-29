# 04: Measure and record in ADR 5

**What to build:** Run the `Performance` test, write the measured time and peak memory into ADR 5 (data access), and confirm the slice 1 tests still pass. If the 5-minute budget or about 1 GB of peak memory is missed, stop and add a fix ticket (COPY inside `ImportWriter`, or a two-pass parser) before closing this one; classification stays untouched.

**Blocked by:** 02 Performance test, 05 Write a vote's rows with COPY (if the budget was missed).

**Status:** ready-for-agent

- [ ] The measured time and peak memory, and where they were measured, are in ADR 5
- [ ] The budget (5 minutes, about 1 GB) is met, or a fix ticket was added and done first
- [ ] All slice 1 tests still pass
