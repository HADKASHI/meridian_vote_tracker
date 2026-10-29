# Slice 9: PDF extraction (bonus)

Status: done
Blocked by: Slice 8
Runs in parallel with: nothing
PR title: `Slice 9: PDF extraction (bonus)`
Requirements: PDF-xx (brief section 8)
User stories in the spec: none yet

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

Extract country votes from the scanned voting sheets into the Review inbox. Designed in [ADR 0008](../../../../docs/adr/0008-pdf-extraction-through-review.md): Document Intelligence reads the sheet, a fixed mapper decides the votes, printed totals gate approval, and approval goes through the importer. Hard limit: half a day. Whatever is not built stays as design in ADR 0008 and DECISIONS.md.

## Lane

A `pdf_extraction` job kind, an extraction record and `/api/extractions`, and an Extractions tab in Review. One migration, flagged in the PR; one small refactor of the job processor in `Meridian.Platform`, in its own commit.

## Data it arranges

The voting sheets are client data: never committed.

## Done when

- every ticket in `issues/` that fits the half day is `Status: done`; the cut order is the ticket order, with 10 first to go, then 9, then 8
- an analyst can upload a sheet, see it checked against its printed totals, fix or reject it, and approve it into the same classification as a CSV (PDF-01 to PDF-06)

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
