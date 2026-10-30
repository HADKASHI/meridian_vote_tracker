# Slice 12: Alignment rules and evidence

Status: done
Blocked by: Slices 6, 7 and 11
Runs in parallel with: slice 8 (not with slice 11: both change the snapshot page and the snapshots module)
PR title: `Slice 12: Alignment rules and evidence`
Requirements: none (an extra)
User stories in the spec: 120–123

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions", then [ADR 0006](../../../../docs/adr/0006-scoring-rules.md) and [ADR 0007](../../../../docs/adr/0007-rule-sets-and-evidence-metrics.md).

## Goal

The admin can score a snapshot under a different, named set of rules, and every score is paired with completeness and confidence metrics, so an analyst can tell a ranking backed by comprehensive voting evidence from one skewed by missing data or heavy abstentions.

## Lane

The pure scoring module in the domain project (rule sets, weight unit, the evidence metrics and the confidence band, all pure and unit-tested), the snapshots feature module (create takes an optional rule set, the job scores under it, results and the comparison return the metrics and the rule set) and the Angular snapshots feature (the new-snapshot form, the snapshot page, the publications page, the comparison screen). No migration: the rule set id is stored in `snapshot.scoring_rules_version`, weights stay integers by a per-rule-set weight unit, and the metrics are derived from the stored lines.

Two things to handle in the lane: the comparison reasons currently restate the default scoring rule (which categories count as matched and counted) and must use the rule set the snapshot was scored under; and the job's check that a snapshot's rules version equals the worker's must accept any known rule set and refuse an unknown one.

## Data it arranges

Snapshots calculated with slice 0's fixture builders over states that vote yes and no, abstain, are absent, have no data and were not members, so each rule set and each confidence band is proven.

## Done when

- the admin can choose a rule set when creating a snapshot (an analyst choosing a non-default one gets 403); the default is `alignment-2026.1`, today's rules, and existing snapshots read exactly as before
- `abstain-as-miss`, `abstain-half-credit` and `strict` score as ADR 0007 says, with stored weights as integers in the rule set's weight unit and ranking on the exact value
- every state's result carries completeness, abstention rate, missing data and a confidence band, derived from the stored lines and the same under every rule set; a state with nothing countable has no band
- the ranking shows the metrics beside each score and a "low evidence" flag, rank is unchanged by confidence, and the ranking can be filtered or sorted by confidence
- the snapshot page, the publications page and the comparison show the rule set name, and the comparison warns when A and B were scored under different rule sets
- the stored snapshot is never recalculated and no migration is added

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
