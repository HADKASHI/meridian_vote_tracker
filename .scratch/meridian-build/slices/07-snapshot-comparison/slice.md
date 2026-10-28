# Slice 7: Snapshot comparison

Status: ready-for-agent
Blocked by: Slice 6
Runs in parallel with: slices 2, 3, 4, 5
PR title: `Slice 7: Snapshot comparison`
Requirements: none (an extra)
User stories in the spec: 102

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

An analyst compares two snapshots and sees each state's rank and score movement.

## Lane

The compare endpoint in the snapshots feature module and `/snapshots/compare` in the snapshots feature folder. A pure read over stored results and lines.

## Data it arranges

Two calculated snapshots arranged with slice 0's fixture builders.

## Done when

- the comparison shows rank and score movement per state, including states n/a in one snapshot
- it reads stored data only and never recomputes

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
