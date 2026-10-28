# Slice 3: Conflicts and the Review inbox

Status: done
Blocked by: Slice 0
Runs in parallel with: slices 1, 2, 4, 5, 6, 7
PR title: `Slice 3: Conflicts and the Review inbox`
Requirements: all CON-xx requirements, including CON-02, CON-03 and CON-05
User stories in the spec: 50–64

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

An analyst works through pending conflicts in the Review inbox and accepts or rejects each with a comment. Exactly one of two simultaneous decisions applies; the loser sees the winner's decision and their attempt is recorded. Every vote-details decision recalculates membership (map ticket 08 rule 8).

## Lane

The conflicts feature module (inbox query, decision use case with its vote lock, not-applied attempts, membership recalculation) and the Angular review feature (`/review`, `/review/:conflictId`). Opening conflicts during an import is slice 1's.

## Data it arranges

Conflicts on ballots and on vote details, arranged with slice 0's fixture builders, so this slice never waits for the importer.

## Done when

- accepting moves the approved value with the decision as its origin and keeps the old revision in history; rejecting changes nothing approved
- the decision race test runs 20 rounds with exactly one 200 and one 409 `already-decided` each, and a not-applied record per loser
- a vote-details decision approves ballots that were invalid only for membership, and stops counting ballots now outside membership
- the 409 card replaces the decision buttons in the UI

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
