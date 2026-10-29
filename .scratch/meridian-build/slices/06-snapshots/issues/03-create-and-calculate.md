# 03: Create and calculate a snapshot

**What to build:** an analyst calculates the previewed snapshot. The create call refuses a selection that matches no votes (`no-votes-match`) or that contains a vote without a position (`no-position`, listing them); otherwise it stores the snapshot header (name, note, selection as entered, creator) and its queued job, and answers 202. The snapshot job reads its facts in one `REPEATABLE READ` transaction from the current views, re-checks positions (failing with the vote and why if one slipped in after the preview), scores them with the pure function of ticket 01, and writes the snapshot's votes (with the details and position revisions used), lines (with the country vote revision used, or none), results and warnings (as they stood, including "calculated while import #N was applying, X of Y votes") in its final transaction, with the scoring rules version on the header. `/snapshots/:snapshotId` follows the job while it runs and then shows the ranking (score to 1 decimal with its basis, n/a last), the warnings, or the failure with its error.

**Blocked by:** 01, 02

**Status:** done

- [x] Create answers 400 `no-votes-match` and 400 `no-position` (with the votes) without creating anything, and 202 with the snapshot and job ids otherwise
- [x] The job stores votes, lines and results matching the scoring function over the approved data, and the header records the scoring rules version
- [x] A vote added to the selection without a position after creation makes the job fail with an error naming the vote
- [x] A snapshot reads the same after the data under it changes (a conflict accepted, a position changed, a new vote in its date range)
- [x] Warnings seen at calculation time are stored on the snapshot and returned with it
- [x] The snapshot page shows progress while the job runs, the ranking with basis and n/a states last when done, and the error when failed
