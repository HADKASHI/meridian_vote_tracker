# 04: Upload a sheet and watch it extract

**What to build:** An analyst uploads a PDF; the request returns at once; a background job reads it through the reader interface (a fake in tests) and the extraction appears in a list with a live status that survives a page refresh, ending in needs review with its rows, header and issues.

**Blocked by:** Job kind policy, Sheet to checked rows

**Status:** done

**Requirements:** PDF-01

- [x] One migration adds the extraction record, the pdf_extraction job kind and the job's link to an extraction; the PR says so at the top
- [x] The PDF is stored in the blob container under its own prefix by the same streaming, hashing helper the CSV upload uses (no second copy of that code)
- [x] POST, list and get under /api/extractions, role-checked like the other write endpoints, with the OpenAPI file regenerated
- [x] The job handler runs the reader, then the sheet-to-checked-rows logic, and saves rows, issues and status
- [x] Status goes extracting then needs review and is returned by get
- [x] Logs and errors hold ids, counts and timings only
