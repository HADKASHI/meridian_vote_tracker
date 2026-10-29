# 10: Preview the classification before approving

**What to build:** Before approving, the analyst sees how many rows would be new, unchanged or in conflict, computed read-only. Cut first if time is short; the page image beside the table joins it if there is time.

**Blocked by:** Approve an extraction through the importer; Extractions tab in Review

**Status:** ready-for-agent

**Requirements:** PDF-05 (stretch)

- [ ] A read-only endpoint returns the counts using the pure classifier against current approved data and writes nothing
- [ ] The detail shows the counts next to Approve
- [ ] The corrigendum fixture previews one conflict
