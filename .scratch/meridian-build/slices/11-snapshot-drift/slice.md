# Slice 11: Snapshot drift

Status: done
Blocked by: Slices 6 and 7
Runs in parallel with: slice 8 (it touches only the snapshots feature)
PR title: `Slice 11: Snapshot drift`
Requirements: none (an extra)
User stories in the spec: 119

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

On a snapshot's page an analyst sees, in a small summary, what would differ if the snapshot were created now: the votes and positions that changed since it was calculated, with no job and no score calculation.

## Lane

A read endpoint `GET /api/snapshots/{snapshotId}/changes` in the snapshots feature module, and a panel on `/snapshots/:snapshotId` in the snapshots feature folder. A pure read over the snapshot's stored revisions and selection and today's current values. It adds no migration, no job and no change to the stored snapshot response.

## Data it arranges

One calculated snapshot arranged with slice 0's fixture builders, then later changes made with the same builders: a new position, an accepted country vote, changed vote details, a new vote in the selected date range, a vote moved out of it.

## Done when

- the panel lists, per kind, the votes whose position, country votes or details changed since the snapshot, the votes now matching its selection that it doesn't hold, and the votes it holds that no longer match, each with the old and the new value and who or what changed it
- it never shows a score or a rank, runs no job and never changes the stored snapshot
- it says plainly when nothing changed, and is labelled live with the time it was read
- it is shown only for calculated snapshots

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
