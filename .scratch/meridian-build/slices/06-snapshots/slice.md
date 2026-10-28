# Slice 6: Snapshots and publication

Status: ready-for-agent
Blocked by: Slice 0
Runs in parallel with: slices 1, 2, 3, 4, 5
PR title: `Slice 6: Snapshots and publication`
Requirements: SNP-01 to SNP-05, POS-04
User stories in the spec: 77–101

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

An analyst previews and calculates a snapshot over a chosen set of votes; it stores its votes, lines and results and never changes. The admin publishes one per reporting quarter and may replace it with a reason. Scoring follows map ticket 05 exactly.

## Lane

The snapshots feature module (preview, create, snapshot job handler, the pure scoring function, publication chain) and the Angular snapshots feature (`/snapshots`, `/snapshots/new`, `/snapshots/:snapshotId`, `/publications`).

## Data it arranges

Approved country votes, vote details, positions and membership periods arranged with slice 0's fixture builders, including states with nothing countable and exact ties.

## Done when

- every numbered rule of map ticket 05 has a named domain test of the scoring function
- votes without a position block the preview and the create call, and the job re-checks them
- a snapshot reads the same after the data under it changes
- the publication race test runs 20 rounds with exactly one success and one 409 `quarter-published` each; a replacement needs a reason and keeps the chain visible

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
