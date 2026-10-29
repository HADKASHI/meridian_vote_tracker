# 06: Edit and reject an extraction

**What to build:** An analyst fixes what the reader got wrong, or rejects the sheet. Saving edits keeps the extracted value beside the edited one with who and when, and re-runs the checks so a fixed mark can clear a blocker.

**Blocked by:** Upload a sheet and watch it extract

**Status:** ready-for-agent

**Requirements:** PDF-04

- [ ] Save edits changes a row's code or choice and the header; the original extracted value, editor and time are kept
- [ ] Validation re-runs on save and the response shows the current issues and totals
- [ ] Reject ends the item with an optional comment and writes nothing to approved data
- [ ] A decision on an already decided extraction returns 409 with the winning decision, proven by a two-connection race test
- [ ] Wrong roles are refused by the generated role matrix
