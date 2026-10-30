# 03: Trim the shipped AI tooling

**What to build:** the repo ships only the AI tooling this project actually used, decided with the user in this slice (map ticket 23's questions):

- **Skills kept** in `.claude/skills/`: wayfinder, grilling, grill-with-docs, research, prototype, domain-modeling, to-spec, to-tickets, triage, build-slice, tdd, code-review, pr and setup-matt-pocock-skills. Every other skill is removed, together with the duplicate `.agents/skills/` copy.
- **`.scratch/` ships** as the evidence trail of the map, research, spec and slice tickets. AI_USAGE.md may point to it; DECISIONS.md and the ADRs never link into it.
- **Sandbox files ship unchanged**: `.claude/sandbox/`, `.claude/cloud-dev-setup.sh` and the SessionStart hook. The README never mentions them.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Only the fourteen kept skills remain, and every skill a kept skill calls is itself kept
- [ ] `.agents/skills/` is gone, and nothing in the repo points to it or to a removed skill
- [ ] `.scratch/`, `.claude/sandbox/`, `.claude/cloud-dev-setup.sh` and the hook are untouched
