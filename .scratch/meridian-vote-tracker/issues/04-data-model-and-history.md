# Design the data model and history

Type: grilling
Status: open

## Question

How does the database represent votes, country votes, member states with membership periods, imports and their per-row outcomes, conflicts, positions and weights, so that HIS-01 to HIS-04 hold: every approved value knows its source, previous values are never lost, every decision and position or weight change records who, when, old and new, and a vote's full timeline can be shown? Options include append-only revision rows with a current pointer, a current table plus an audit log, or event sourcing. Constraints and indexes must protect integrity (brief section 9).
