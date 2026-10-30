# Scanned voting sheets are read by Document Intelligence, checked against their printed totals and approved by a person

Status: accepted

## Context

The bonus (brief section 8, PDF-01 to PDF-06) extracts country votes from scanned voting sheets. A sheet is an image-only scan, one 200 dpi JPEG per page with no text layer: a header (vote no., session, date such as "4 October 2011", resolution), a grid of printed three-letter code, member state and three columns (YES, NO, ABST.) with a hand-drawn X, and a footer of recorded totals (yes, no, abstentions, not voting). A blank row means the state did not vote. The scans are skewed and speckled, and the audit's sheets also carry circled or struck-out marks, two marks on one row and states that are not in `member_states`. A later sheet can correct an earlier one (a corrigendum for the same vote). The sheets are client data and never committed.

## Decision

- **Document Intelligence reads the page; plain C# decides what it means.** The Azure adapter calls Document Intelligence's `prebuilt-layout` model and hands back pages of cells with their text and confidence. A deterministic mapper turns each grid row into a country vote: the code comes from the printed Code column, the mark's column gives YES to `Y`, NO to `N`, ABST. to `A`, and a blank row gives `X` (did not vote), the same encoding as the CSV. The header gives vote id, session, title and date, and the date is normalised to ISO form. No language model is used: the grid is rigid, the only judgement is which of three cells holds a mark, and a model adds cost and plausible-looking wrong votes.
- **The reader is one narrow interface** (sheet bytes in, pages of cells with confidence out). The Azure adapter is its only real implementation, built on an instrumented `HttpClient` inside the job's trace; endpoint and key come from configuration (`.env`, user-secrets), never from the repository. Tests and CI use a fake reader; the adapter's response parsing is tested on a hand-written response fixture, and a `RealData` smoke run uses real credentials and real sheets.
- **Validation runs before anything reaches a person.** Blocking issues keep Approve disabled until fixed or rejected: counts do not equal the printed totals for any of yes, no, abstentions or not voting; a country code is unknown or was not a member on the vote date (the CSV import's own membership check, not a second one); a code appears twice; a row has two marks or an unreadable cell. Warnings are shown but do not block: a low-confidence cell, a header vote id that differs from the file name, a member on the date with no row on the sheet. Printed totals are the main control, so a missed or invented mark shows as a mismatch. A mismatch still blocks, but an analyst who has checked the original can accept the counts as they stand: that is recorded as an edit with both sets of totals, shows afterwards as a warning, and holds only for exactly those counts, so changing a row or the printed totals asks again.
- **An extraction is its own record, not an import.** A new `extraction` table holds the stored PDF (blob name, SHA-256, size, uploader), a status (extracting, needs review, failed, approved, rejected), the header, the extracted rows with each cell's confidence, the issues, the analyst's edits (the original value is kept beside the edited one, with who and when; a removed row is kept as an edit saying what it was) and the decision. A migration adds it, adds `pdf_extraction` to the job kinds and gives the job an `extraction_id` link. The PDF goes into the existing blob container under `sheets/{id}/`, written by the same streaming, hashing helper the CSV upload uses. Uploading the same bytes again returns the existing extraction.
- **The job kind is a third copy of nothing.** The job processor's per-kind rules (concurrency cap in the claim, timeout, log scope key) move into one small per-kind policy that import, snapshot and PDF extraction all read. This is a refactor of working code, in its own commit and flagged in the PR.
- **Approval is the only write, and it goes through the importer.** Approving creates one `Import` that points at the already-stored PDF (no copy) and feeds the approved rows to the existing import writer as rows of a file, so classification, conflicts, history and origins are the CSV's, not a second implementation. A corrigendum is then nothing special: it is another sheet for the same vote, and any changed state becomes a conflict for a person to decide (the 2011 corrigendum changes Kenya from YES to ABSTAIN, so totals 70, 55, 7, 16 become 69, 55, 8, 16 and approving it yields one conflict). The approval screen can preview "new, unchanged, conflicts" by running the pure classifier read-only.
- **Review decisions work like conflict decisions.** An analyst or admin can save edits (validation re-runs and may clear blockers) and can remove a row that has an issue, which treats it as never read: it leaves the counts, the checks and the import, and the printed totals still gate approval. A row without an issue can't be removed, so a clean row is never dropped to make the totals fit. They can also approve (only with no blocking issue) or reject (with an optional comment). A second decision on the same item is refused with 409 and the winning decision. Nothing reaches approved data without a decision.
- **Failure is shown, never fatal (PDF-06).** Transient reader errors use the job's retries. A permanent failure (timeout, not a PDF, empty or protected file, a 4xx from the service) sets the extraction to failed with a message-only error that holds no document content, and the item offers Retry, which queues a fresh job. Status is visible while extracting and survives a refresh.
- **The API and screen stay in their lane.** `/api/extractions` carries upload, list, get, save edits, approve, reject and retry; the conflict endpoints are untouched. The Review page gets Conflicts and Extractions tabs with counts, built on the page's existing list and detail layout. Showing the page image beside the table is a stretch.
- **Build order is the cut line.** Mapper and validators, then the job, then the endpoints and approval, then the Azure adapter, then the screen, with the classification preview and page image last. Whatever is not built stays in this ADR and DECISIONS.md as design.

## Considered options

- **A vision language model (or both):** it reads anything, but its output is unrepeatable and can invent a mark; the totals check would catch some of that, not all. Kept as a possible fallback for sheets the layout model cannot read.
- **Local OCR (Tesseract):** nothing in the image, and the README asks for an Azure AI service.
- **A new revision origin for extractions:** a bigger migration through every consumer of origins, and a second path to the same classification.
- **Folding extractions into the conflict list:** it would change a query and DTO that slice 3 owns and mix two kinds of decision in one list.
- **Approving an extraction without review when confidence is high:** the brief says an AI result is never trusted without a check.

## Known limits

- **An analyst corrects or removes rows but cannot add one.** A state the reader missed has to come from a corrected sheet or a CSV; a removed member shows as a missing-member warning.
- **A failed sheet can be retried but not dismissed;** only a sheet waiting for review can be rejected, so a permanently failed one stays in the Failed list.
- **Only a totals mismatch can be acknowledged;** other warnings (low confidence, vote number, missing member) are not, and a low-confidence row can be approved with the sheet.
- **Accepting a mismatch is an analyst's word,** with no second person and no required reason; the edit history shows who and when.
- **The Document Intelligence request and response shapes are the documented v4 ones, proven only against a hand-written fixture.** The RealData smoke test, which needs real credentials and real sheets, is the first live check (the operator reports Document Intelligence working against the live service; ADR 0009). Whether the layout model returns a drawn X as text, as a selection mark or not at all is unknown until then; the mapper counts any text in a vote column as a mark.
- **Known bug: a sheet uploaded from the Imports page does not appear in the Extractions list.** Reported by the operator: the sheet is uploaded, but its extraction does not show in the list. Whether uploading from the Review page is affected, and the cause, have not been checked; it is not fixed.
- **The page image beside the table is not built.**

## Consequences

- One migration, flagged at the top of the slice's PR.
- `Meridian.Platform`'s job code changes shape once; import and snapshot jobs must behave as before.
- The CSV upload's blob code becomes a shared helper.
- A PDF-originated import is an `Import` whose file is a PDF; the imports list, reports and origin evidence must tolerate a PDF file with rows numbered by sheet order.
- Real sheets never leave `candidate_data/` or the blob container; logs hold ids, counts, model, latency and outcome only.
