# Slice 2: Import at scale

Status: ready-for-agent
Blocked by: Slice 1
Runs in parallel with: slices 3, 4, 5, 6, 10
PR title: `Slice 2: Import at scale`
Requirements: the 5-minute budget for the historic file (brief section 9)
User stories in the spec: 17

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

Prove the ~1M-row historic import finishes in under 5 minutes in compose on a laptop, and fix it only if it doesn't.

## Lane

The deterministic synthetic generator, the `Performance` test category, the manual GitHub workflow that reports time and peak memory, and, only if the budget or about 1 GB of memory is missed, the internals of `ImportWriter` (COPY) or the parser (two passes). Classification stays untouched.

## Data it arranges

The generator's file, shaped like the historic audit (map ticket 17): 6,480 interleaved votes of 147–151 rows with a scattering of each defect kind. Never committed: generated on demand from a fixed seed.

## Done when

- the `Performance` test runs on demand, fails above 5 minutes and reports time and peak memory
- the measured numbers are written into ADR 5 (data access)
- all slice 1 tests still pass

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
