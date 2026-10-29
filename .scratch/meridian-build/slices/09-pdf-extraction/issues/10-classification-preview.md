# 10: Preview the classification before approving

**What to build:** Before approving, the analyst sees how many rows would be new, unchanged or in conflict, computed read-only. Cut first if time is short; the page image beside the table joins it if there is time.

**Blocked by:** Approve an extraction through the importer; Extractions tab in Review

**Status:** done

**Requirements:** PDF-05 (stretch)

- [x] A read-only endpoint returns the counts using the pure classifier against current approved data and writes nothing
- [x] The detail shows the counts next to Approve
- [x] The corrigendum fixture previews one conflict

Not built: the page image beside the table (the stretch the ticket named). It stays as design in ADR 0008 and DECISIONS.md.
