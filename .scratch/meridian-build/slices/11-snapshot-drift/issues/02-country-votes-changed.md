# 02: Country votes changed since a snapshot

**What to build:** the changes answer also lists the country votes that changed: for each vote and member state the snapshot used, where the approved ballot is no longer the revision the snapshot stored. The kind has its real count and the first 20, with the old and the new ballot, who or what changed it (import row and uploader, or decision and who made it) and when. A state the snapshot held as no data that now has an approved ballot counts as changed.

**Blocked by:** 01

**Status:** done

- [x] Country votes changed has its count and the first 20 entries, each with vote, state, old ballot (or none) and new ballot, origin and when
- [x] An accepted conflict decision shows the decision and who decided it as the origin; an import row shows the import and its uploader
- [x] A state with no data in the snapshot and an approved ballot now is listed with an old value of none
- [x] An unchanged ballot, a pending conflict and an approved value for a vote the snapshot does not hold are not listed
- [x] Integration tests through the HTTP API prove the above against fixture-built snapshots

