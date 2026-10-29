# 04: Imports list and import report

**What to build:** the Imports page and the import report look finished. The list shows each import with its job-state chip and outcome counts, beside an upload card. The report opens with the file's evidence and the job's progress bar while it runs; its counts per outcome are filter chips; warnings use the problem-banner tone and collapse long lists of missing states; conflicts opened link to the Review inbox; counts per vote and the row records sit in tables that scroll inside their own container.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] Imports list in a card with the upload action, job-state chips and outcome chips, plus empty, loading and rejected states
- [ ] Report header with breadcrumb to Imports, evidence, job-state chip and progress bar while queued or running
- [ ] Counts per outcome as Material filter chips, the selected one marked by more than colour
- [ ] Warnings name the vote and collapse a long missing-state list to its count
- [ ] Row records with outcome chips; long raw text wraps; tables scroll inside their own container at 390 px
- [ ] Existing imports tests pass with their `data-testid`s
