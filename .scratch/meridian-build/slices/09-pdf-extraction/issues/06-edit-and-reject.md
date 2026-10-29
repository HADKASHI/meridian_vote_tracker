# 06: Edit and reject an extraction

**What to build:** An analyst fixes what the reader got wrong, or rejects the sheet. Saving edits keeps the extracted value beside the edited one with who and when, and re-runs the checks so a fixed mark can clear a blocker.

**Blocked by:** Upload a sheet and watch it extract

**Status:** done

**Requirements:** PDF-04

- [x] Save edits changes a row's code or choice, the header and the printed totals (needed when the footer can't be read); the original extracted value, editor and time are kept
- [x] Validation re-runs on save and the response shows the current issues and totals
- [x] Reject ends the item with an optional comment and writes nothing to approved data
- [x] A decision on an already decided extraction returns 409 with the winning decision, proven by a two-connection race test
- [x] Wrong roles are refused by the generated role matrix
