# Slice 4: Vote page and timeline

Status: ready-for-agent
Blocked by: Slice 0
Runs in parallel with: slices 1, 2, 3, 5, 6, 7
PR title: `Slice 4: Vote page and timeline`
Requirements: HIS-01 to HIS-04
User stories in the spec: 65–71

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

Anyone finds a vote and sees its approved details, position and country votes, each value with its origin, and its full timeline, paged, with from → to pairs built by the API.

## Lane

The votes feature module (search, vote page, timeline over the `vote_timeline` view, from → to pairing in C#) and the Angular votes feature (`/votes`, `/votes/:voteId`). The position shows read-only; editing it is slice 5's.

## Data it arranges

A vote with every kind of timeline event (import, approved change, conflict opened and decided, losing attempt, position change, snapshot and publication), arranged with slice 0's fixture builders.

## Done when

- the timeline shows every event kind of map ticket 04 rule 8, ordered by history id, paged
- from → to pairs are correct for country votes, vote details and positions
- every approved value names its origin (import row with file, line and uploader, or the decision)

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
