# 04: Drift panel on the snapshot page

**What to build:** on `/snapshots/:snapshotId` an analyst sees a small summary of what would differ if the snapshot were created now: per kind the count and the first votes with the old and the new value and who or what changed it. It says plainly when nothing changed, is labelled live with the time it was read, shows no score or rank, and appears only for a calculated snapshot.

**Blocked by:** 02, 03

**Status:** done

- [x] The panel lists the five kinds (position, country votes and details changed; now selected but not in the snapshot; no longer selected), each with its count and entries, the old and new value, and who or what changed it
- [x] A kind with more than the 20 returned shows its real count and says only the first 20 are listed
- [x] When nothing changed the panel says so plainly instead of showing empty lists
- [x] It is labelled live and shows the time it was read
- [x] It shows no score or rank, and is not shown while the snapshot is queued, running or failed
- [x] Each listed vote links to its vote page
- [x] Frontend tests prove the above; the generated client is regenerated from the OpenAPI file
