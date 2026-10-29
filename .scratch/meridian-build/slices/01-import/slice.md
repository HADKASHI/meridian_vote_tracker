# Slice 1: Import

Status: done
Blocked by: Slice 0
Runs in parallel with: slices 3, 4, 5, 6
PR title: `Slice 1: Import`
Requirements: ING-01 to ING-15
User stories in the spec: 9–14, 18–49

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

An analyst uploads a file and gets a permanent, correct import report: every row with one outcome and reason, new values approved with their origin, conflicts opened (deciding them is slice 3), warnings, live progress, crash resume. Classification follows map ticket 08 exactly (with `resolution_title`, map ticket 17).

## Lane

The imports feature module (upload endpoint, import job handler, parser, classification, `ImportWriter`), the domain tests for parsing, normalisation and classification, and the Angular imports feature (`/imports`, `/imports/:importId`).

## Data it arranges

Committed CSV fixtures, one small file per import rule, plus the brief's 7.2 example.

## Done when

- the brief's 7.2 example passes through the API: upload, job, report, outcome counts, approved values and the conflict
- every numbered rule of map ticket 08 has a named domain test
- a retried import resumes from its committed votes and ends with the same report as an uninterrupted run
- re-uploading the same file is warned as identical and changes nothing
- the `RealData` test gives the audited counts when `candidate_data/` is present (it skips itself otherwise)

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
