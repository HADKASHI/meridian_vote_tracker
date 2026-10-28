# Slice 0: Walking skeleton

Status: done
Blocked by: none
Runs in parallel with: slices none (the shared base: build and merge it alone, before any other slice)
PR title: `Slice 0: Walking skeleton`
Requirements: Section 3 (roles), section 7.7 (generated client), section 9 (integrity, data access), the 501 contract for every ING, CON, HIS, POS and SNP endpoint
User stories in the spec: 1–8, 15, 16, 103–118

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

Make `docker compose up --build` on a clean clone run the whole system with no feature in it yet, and fix every shared piece the parallel slices would otherwise collide on. Everything listed under slice 0 in the spec's "Build order and slices" is in scope here; that list is the checklist.

## Lane

The whole repository: this slice creates the solution, both test projects, the web app, compose, CI and the ADRs. It leaves the feature modules and Angular feature folders as stubs (501s and placeholder pages) for the slices that own them.

## Data it arranges

Synthetic member states (`tests/data/`) for tests and CI. The fixture builders this slice ships are what every later slice arranges its data with: approved country votes and vote details (with a fixture import as their origin), pending and decided conflicts, positions, and calculated snapshots.

## Done when

- `docker compose up --build` on a clean clone starts all six services; `migrate` fails with a clear message when `member_states.csv` is missing
- every endpoint in the contract exists with its final route and DTOs and returns 501 ProblemDetails; the role-matrix test passes for all of them
- the guard test passes: every table outside the allowlist refuses UPDATE, DELETE and TRUNCATE
- a no-op job queued through the runner reaches `done`, shows in `GET /api/status` and the header, and a killed worker's job is requeued by the sweeper
- the Angular shell switches users, reaches every route, and shows the jobs indicator and Review badge
- a sample integration test uses the template database and a fixture builder; CI's three jobs are green, including the OpenAPI drift check
- the six ADRs and the schema document exist in `docs/`

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
