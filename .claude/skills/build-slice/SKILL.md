---
name: build-slice
description: "Build one slice of a sliced feature end to end in this session and open its PR. Argument: the slice folder."
disable-model-invocation: true
---

# Build a slice

Take one slice from its brief to a green PR into `main` whose commits tell the slice's story. The argument is a slice folder, e.g. `.scratch/meridian-build/slices/03-conflicts`. Invoking this skill is the user's request to commit, push this session's branch and open the slice's PR.

The rules this skill applies live in the feature's `spec.md`, under **Delivery** (lane, commits, PR, merging) and **Testing Decisions** (seams). Read both before step 3; this skill only orders the work.

## 1. Orient

Read the slice's `slice.md`, the spec, `CLAUDE.md`, `GLOSSARY.md`, `docs/agents/issue-tracker.md`, the ADRs in `docs/adr/` and every map ticket the brief cites.

Done when you can state the slice's goal, lane and done criteria in your own words.

## 2. Check it is unblocked, and start from `main`

Fetch `main`. Every slice in the brief's `Blocked by` line must show `Status: done` in its `slice.md` on `origin/main`. If one doesn't, stop and tell the user which slice is missing.

Put this session's own branch on `origin/main`: if the branch has no commits beyond `main`, reset it there; if it has, rebase them onto it. Every later push goes to this branch.

Done when `git log origin/main..HEAD` shows only this slice's commits (or none).

## 3. Tickets

If `issues/` holds no tickets yet, read `.claude/skills/to-tickets/SKILL.md` and follow it with `slice.md` as the source, publishing into this slice's `issues/`. Its step 4 asks the user to approve the breakdown; wait for that approval. Commit the tickets as the slice's first commit: `Add slice N tickets`.

Done when every ticket file exists with its blocking edges and the commit is made.

## 4. Build, one ticket per commit

Work the frontier of the slice's tickets in order. For each ticket:

1. Call the Skill tool with `tdd`. The seams are pre-agreed in the spec's Testing Decisions; that agreement is the confirmation `tdd` asks for.
2. Run the checks a contributor runs locally (build, the tests the ticket touches, lint, the OpenAPI drift check when the contract moved).
3. Set the ticket's `Status: done` and commit the ticket, its tests and that status line together, with the commit rules from Delivery. Fold a fix to the ticket you are on into its commit with `git commit --amend` before it is pushed.

Push after each ticket so the user can follow along. Once pushed, a commit stays as it is.

Done when every ticket is `Status: done` and every acceptance criterion in it is ticked.

## 5. Review and catch up with `main`

1. Call the Skill tool with `code-review` against `origin/main`. Fix each real finding as its own small commit that says what it fixes.
2. Fetch `main` and rebase onto it. Resolve the OpenAPI file by regenerating it, never by hand. Run the full test suite.
3. Set `Status: done` in `slice.md` and commit it with the last change, or alone as `Close slice N`.
4. Push with `--force-with-lease` (the rebase rewrote only this session's own branch).

Done when the full suite is green on top of the current `origin/main` and the branch is pushed.

## 6. Open the PR and drive it to green

Open a PR from this branch into `main`, titled as the brief's `PR title` line. Call the Skill tool with `pr` for the body, and make it list the requirement IDs, the tickets closed and the tests that prove them. Note anything the slice could not keep in its lane (a contract change outside its endpoints, a needed migration) at the top of the body.

Subscribe to the PR's activity and stay with it: CI failures and review comments are fixed as new commits on this branch. The user merges it with rebase-merge.

Done when CI is green on the PR's head and no review thread waits on you.
