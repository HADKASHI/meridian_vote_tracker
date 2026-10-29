# 03: Stale save in the UI

**What to build:** when the admin's save comes back as `stale-position`, the row shows the 409 card in place of the save button, with the current position ("Lena set Yes · weight 4 at 14:02 while you edited"). "Use theirs" drops the edit and shows the current position; "Save mine over it" resends the admin's stance and weight with the current revision id as the version. No toast, no automatic retry.

**Blocked by:** 02

**Status:** done

- [x] A 409 `stale-position` replaces the row's save action with the card showing who set what, and when
- [x] "Use theirs" shows the current position and leaves the row out of edit mode
- [x] "Save mine over it" resends with the current revision id and the row shows the saved position
- [x] Component tests with `HttpTestingController` cover both actions
