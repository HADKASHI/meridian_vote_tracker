# 09: Extractions tab in Review

**What to build:** In Review the analyst switches between Conflicts and Extractions, uploads a sheet, sees its status, checks the table against the printed totals and acts on it.

**Blocked by:** Failure, retry and duplicates; Edit and reject; Approve through the importer

**Status:** ready-for-agent

**Requirements:** PDF-01, PDF-03, PDF-04, PDF-05, PDF-06

- [ ] Review has Conflicts and Extractions tabs with counts, built on the page's existing list and detail layout
- [ ] Upload shows progress and survives refresh; a failed item shows its error and a Retry button
- [ ] The detail shows vote id, date and title, the rows with extracted value, confidence and issue, and a printed-versus-extracted totals panel
- [ ] Rows can be edited and saved; Approve is disabled while a blocking issue remains; Reject takes a comment; a 409 shows the winning decision as a clear message
- [ ] Vitest specs cover the tab, the disabled Approve and the 409 card, with lint and build green
