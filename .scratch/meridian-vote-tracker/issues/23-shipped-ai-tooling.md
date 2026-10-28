# Decide what AI tooling ships with the submission

Type: grilling
Status: resolved
Blocked by: 12

## Question

Which skills in `.claude/skills/` stay in the submitted repo (only those this project actually used), and what happens to `.scratch/` (the wayfinder map and research)? How does AI_USAGE.md describe the workflow, and where AI output was checked, rejected or corrected? Is `.claude/cloud-dev-setup.sh` kept? Needed before the finishing pass in [Set the build order](12-build-order.md).

## Comments

- From [Design Docker Compose and image builds](18-docker-compose.md): decide whether the sandbox-only proxy override under `.claude/sandbox/` ships.

## Answer

**Ruled out of scope for this map** by the user. Which skills, scratch notes and sandbox files ship can only be judged once development is finished, so it is decided in the finishing pass of [Set the build order](12-build-order.md), not here. The questions above and the comment about `.claude/sandbox/` carry over to that pass unchanged.
