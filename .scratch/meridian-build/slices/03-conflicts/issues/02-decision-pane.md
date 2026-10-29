# 02: Decision pane shows both values with their origins

**What to build:** selecting a conflict in the Review inbox opens the decision pane beside the list at `/review/:conflictId`. It shows the current approved value with its origin (the file, line and uploader of the import row that brought it, or the decision that approved it) and the incoming value with its origin (the first file row that brought it: file, line, uploader), so the analyst decides on evidence. A decided conflict stays viewable: the pane then shows its decision (who, when, accept or reject, from → to, comment) and any decision attempts that were not applied.

**Blocked by:** 01

**Status:** done

- [x] `GET /api/conflicts/{conflictId}` returns the conflict with both sides and their origins, the revision it was compared against, its decision if any, and its not-applied attempts; 404 `not-found` for an unknown id (CON-02; stories 52, 63)
- [x] The current side's origin is an import row or a decision; the incoming side's origin is the earliest row linked to the conflict
- [x] An incoming value not yet approved has no revision id; once accepted, the incoming side names the revision the accept created
- [x] The pane renders at `/review/:conflictId` beside the list, for pending and decided conflicts
- [x] Tests: API tests for both origin kinds, a decided conflict, and 404; a Vitest test for the pane
