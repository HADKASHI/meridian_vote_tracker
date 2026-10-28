# Slice 5: Positions and weights

Status: done
Blocked by: Slice 0
Runs in parallel with: slices 1, 2, 3, 4, 6, 7
PR title: `Slice 5: Positions and weights`
Requirements: POS-01 to POS-04
User stories in the spec: 72–76

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

The admin sets each vote's stance and weight from a worklist, votes without a position first. A stale save is refused with the current value shown.

## Lane

The positions feature module (worklist, conditional pointer update with the revision id as version) and the Angular positions feature (`/positions`).

## Data it arranges

Votes with and without positions, arranged with slice 0's fixture builders.

## Done when

- an analyst gets 403 on every position write; the admin can set stance and weight
- the stale-position race test runs 20 rounds with exactly one success and one 409 each
- "Use theirs" and "Save mine over it" work in the UI

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
