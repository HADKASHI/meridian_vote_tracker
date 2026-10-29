# 04: Show why a state moved on the comparison screen

**What to build:** on `/snapshots/compare` each state row can be opened to list its reasons: the vote, and in plain words what differed between A and B ("only in B", "ballot Y → N", "match → mismatch", "weight 2 → 5"). A state whose rank changed but whose score did not says that its rank moved because other states' scores moved. The open rows live in the query string so a comparison can be linked.

**Blocked by:** 03 Say why a state moved between two snapshots

**Status:** done

- [x] Each state with reasons has a control that opens its list, reachable and operable by keyboard, and states with none show no control
- [x] Each reason shows the vote (assembly vote id and title) and what changed with both values, in words as well as arrows, using glossary terms
- [x] A state with a rank change but no score change says its rank moved because other states' scores moved
- [x] The opened states are held in the query string and survive a reload
- [x] Vitest specs with `HttpTestingController` cover a ballot change, a position change, a vote only in one snapshot, the rank-only case and the query string
