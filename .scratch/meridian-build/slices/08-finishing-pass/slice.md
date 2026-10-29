# Slice 8: Finishing pass

Status: ready-for-agent
Blocked by: Slices 0–7 and 10
Runs in parallel with: nothing
PR title: `Slice 8: Finishing pass`
Requirements: the brief's submission requirements (README, DECISIONS.md, AI_USAGE.md)
User stories in the spec: 112 (verified on a clean clone)

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

Make the submission ready: a clean clone runs, the documents are final, and the AI tooling that ships is chosen.

## Lane

README; DECISIONS.md rewritten to the outline of map ticket 22 (about 1,000 words, printed to A4 to check 2 pages); AI_USAGE.md; the skills trim and the questions of map ticket 23; the Playwright smoke test (switch user, upload, report, two-tab decision race) added to CI.

## Data it arranges

Committed synthetic files only.

## Done when

- `docker compose up --build` works on a fresh clone following only the README
- DECISIONS.md fits 2 printed pages and links its ADRs, never `.scratch/`
- the Playwright smoke test is green in CI

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
