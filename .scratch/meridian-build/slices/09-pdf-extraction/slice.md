# Slice 9: PDF extraction (bonus)

Status: needs-info
Blocked by: Slice 8
Runs in parallel with: nothing
PR title: `Slice 9: PDF extraction (bonus)`
Requirements: PDF-xx (brief section 8)
User stories in the spec: none yet

Read [the spec](../../spec.md) first, especially "Build order and slices", "Delivery" and "Testing Decisions".

## Goal

Extract country votes from the scanned voting sheets into the Review inbox. Not designed yet: the first ticket is a design session (Document Intelligence, an LLM or both; validation against printed totals; the inbox item; retries and failure). Hard limit: half a day. If it doesn't fit, DECISIONS.md describes the design instead.

## Lane

A `pdf_extraction` job kind and a new Review inbox item kind, both of which slice 0 left room for.

## Data it arranges

The voting sheets are client data: never committed.

## Done when

- decided by the design ticket

## Tickets

In `issues/` beside this file, one per ticket, written by `/to-tickets` from this brief. The rules for building, committing and opening the PR are in the `build-slice` skill.
