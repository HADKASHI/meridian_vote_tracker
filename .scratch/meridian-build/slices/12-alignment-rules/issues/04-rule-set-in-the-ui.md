# 04: Choose the rule set in the new-snapshot form and show it everywhere

**What to build:** the admin picks a rule set in the new-snapshot form (default `alignment-2026.1`); an analyst sees only the default. The snapshot page names the rule set and shows weights divided by the weight unit. The publications page names the rule set of each published snapshot.

**Blocked by:** 01 Create a snapshot under a chosen rule set

**Status:** done

- [x] The new-snapshot form offers the rule sets with their names to the admin, defaulting to the default one, and sends the choice; an analyst sees no choice
- [x] The snapshot page shows the rule set name and shows weights in weight, not points
- [x] A publication carries its snapshot's rule set name and the publications page shows it
- [x] An existing snapshot shows `alignment-2026.1` and reads as before
- [x] Vitest specs cover the form for the admin and the analyst, and the snapshot and publications pages
- [x] An integration test proves the publications read carries the rule set name
- [x] The OpenAPI file is regenerated for these endpoints only
