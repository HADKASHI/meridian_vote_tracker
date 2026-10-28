# 02: Preview a selection

**What to build:** an analyst on `/snapshots/new` enters a name, an optional note and a selection (an inclusive date range on approved dates, one or more sessions, or an explicit list of votes) and previews it before calculating. The preview runs no job: it returns how many votes the selection resolves to, the votes without a position (which block the snapshot), and warnings that never block: pending conflicts on selected votes, incomplete votes (members on the vote date with no approved country vote), and an import currently running with its progress. "Remove these votes" turns the selection into the rest, recorded as the original criteria minus those votes; "Check again" re-runs the preview after the admin sets the missing positions. A selection that matches no votes is refused with `no-votes-match`.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] A date range resolves inclusively on each vote's approved date; sessions and an explicit list resolve as entered; removed votes are left out of all three
- [x] A selection matching no votes answers 400 `no-votes-match`; an invalid selection answers 400 `validation` with camelCase keys
- [x] Votes without a position are listed (internal id and assembly vote id), and the count still includes them
- [x] Warnings name each selected vote with pending conflicts (with how many), each incomplete vote (with the missing member state codes), and a running import (its id and votes done of total)
- [x] The screen shows the count, the blocking list with "Remove these votes", "Check again", and the warnings; Calculate stays disabled while any vote has no position
