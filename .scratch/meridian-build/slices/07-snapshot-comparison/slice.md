# Slice 7: Snapshot comparison

Status: done
Blocked by: Slices 6 and 10
Runs in parallel with: slice 2
PR title: `Slice 7: Snapshot comparison`
Requirements: none (an extra)
User stories in the spec: 102

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

An analyst compares two snapshots and sees each state's rank and score movement, and why each score moved.

## Lane

The compare endpoint in the snapshots feature module and `/snapshots/compare` in the snapshots feature folder. A pure read over stored results, lines, votes and the ballots they used.

## Data it arranges

Two calculated snapshots arranged with slice 0's fixture builders.

## Done when

- the comparison shows rank and score movement per state, including states n/a in one snapshot
- the comparison says why each score moved: the votes the two snapshots treated differently, with what changed
- it reads stored data only and never recomputes

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
