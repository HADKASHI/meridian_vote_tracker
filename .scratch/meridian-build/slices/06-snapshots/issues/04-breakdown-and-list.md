# 04: Breakdown per state and the snapshots list

**What to build:** an analyst opens one member state in a snapshot and sees every vote it covered with its category (match, mismatch, abstain, absent, non-member, no data), the stance and weight used, and the exact country vote used, so every score can be recomputed by hand. The `/snapshots` list shows every snapshot, newest first, with name, creator, time, vote count (once calculated), job state and a publication badge.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] The breakdown returns the state's result and one line per snapshot vote, with the details, position and country vote revisions the snapshot stored, not the current ones
- [ ] An unknown snapshot or a state not in the snapshot answers 404
- [ ] The list is paged, newest first, with vote count null until calculated and the snapshot's publications
- [ ] The snapshot page opens a state's breakdown from the ranking; the list page links each snapshot and shows its badge
