# Decide what AI tooling ships with the submission

Type: grilling
Status: open
Blocked by: 12

## Question

Which skills in `.claude/skills/` stay in the submitted repo (only those this project actually used), and what happens to `.scratch/` (the wayfinder map and research)? How does AI_USAGE.md describe the workflow, and where AI output was checked, rejected or corrected? Is `.claude/cloud-dev-setup.sh` kept? Needed before the finishing pass in [Set the build order](12-build-order.md).

## Comments

- From [Design Docker Compose and image builds](18-docker-compose.md): decide whether the sandbox-only proxy override under `.claude/sandbox/` ships.
