# 01: Create a snapshot under a chosen rule set

**What to build:** the admin creates a snapshot under a named rule set (ADR 0007) and the job scores it under exactly that rule set. The four rule sets are `alignment-2026.1` (the default, today's rules), `abstain-as-miss`, `abstain-half-credit` and `strict`. Each declares a weight unit, and matched and counted weight are stored as integer points of 1 ÷ unit of a weight, so a half-credit abstention on a weight-1 vote is 1 of 2 points. The score is still 100 × matched ÷ counted, ranked on the exact value. An analyst creates with the default only. A snapshot's rule set id is stored in its existing rules-version column, so there is no migration, and a snapshot already stored reads exactly as before.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The pure scoring module has the four rule sets, each with its weight unit, and scores a set of facts under any of them; the default rule set gives the same lines, weights and ranks as today
- [ ] A state outside its membership and a member with no data are never counted under any rule set
- [ ] `abstain-as-miss`, `abstain-half-credit` and `strict` count abstentions and absences as ADR 0007 says, with integer weights in the rule set's unit and ties ranked on the exact fraction
- [ ] Domain unit tests name each rule set's rule, including a half-credit tie that only exact comparison gets right
- [ ] `POST /api/snapshots` takes an optional rule set id; omitted means `alignment-2026.1`; an unknown id answers 400 `validation` on the rule set field
- [ ] An analyst choosing a non-default rule set gets 403 and nothing is created; the admin may choose any; an analyst choosing the default explicitly may
- [ ] The job scores under the snapshot's stored rule set and accepts any known one; it refuses an unknown one, and the stored snapshot is never recalculated
- [ ] A snapshot returns its rule set's id, name and weight unit; results carry weights in points of that unit
- [ ] Integration tests through the HTTP API calculate snapshots over fixture-built states that vote yes and no, abstain, are absent, have no data and were not members, under each rule set, and prove scores, weights and ranks
- [ ] The OpenAPI file is regenerated for these endpoints only; no migration is added
