# 03: Trim the shipped AI tooling

**What to build:** the repo ships only the AI tooling this project actually used, decided with the user in this slice (map ticket 23's questions):

- **Skills kept** in `.claude/skills/`: wayfinder, grilling, grill-with-docs, research, prototype, domain-modeling, to-spec, to-tickets, triage, build-slice, tdd, code-review, codebase-design (which tdd calls), pr and setup-matt-pocock-skills. Every other skill is removed, together with the duplicate `.agents/skills/` copy.
- **`.scratch/` ships** as the evidence trail of the map, research, spec and slice tickets. AI_USAGE.md may point to it; DECISIONS.md and the ADRs never link into it.
- **Sandbox files ship unchanged**: `.claude/sandbox/`, `.claude/cloud-dev-setup.sh` and the SessionStart hook. The README never mentions them.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] Only the fifteen kept skills remain, and every skill a kept skill calls is itself kept
- [x] `.agents/skills/` is gone, and nothing in the repo points to it or to a removed skill
- [x] `.scratch/`, `.claude/sandbox/`, `.claude/cloud-dev-setup.sh` and the hook are untouched

## Comments

- The removal of the unused skills and `.agents/skills/` was staged early by mistake and landed in the ticket 01 commit ("Add the Playwright smoke test to CI"). The user chose to keep that pushed commit as it is; this ticket's commit carries the rest.
