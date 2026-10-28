# Issue tracker: Local Markdown

Issues and specs for this repo live as markdown files in `.scratch/`.

## Conventions

- One feature per directory: `.scratch/<feature-slug>/`
- The spec is `.scratch/<feature-slug>/spec.md`
- Implementation issues are one file per ticket at `.scratch/<feature-slug>/issues/<NN>-<slug>.md`, numbered from `01`, never a single combined tickets file
- Triage state is recorded as a `Status:` line near the top of each issue file (see `triage-labels.md` for the role strings)
- Comments and conversation history append to the bottom of the file under a `## Comments` heading

## Sliced features

A feature built in parallel is cut into **slices**: `.scratch/<feature-slug>/slices/<NN>-<slug>/slice.md` is the slice's brief (Status, Blocked by, requirements, lane, done criteria), and its tickets live beside it in `slices/<NN>-<slug>/issues/<NN>-<slug>.md`, numbered from `01` within the slice. `.scratch/meridian-build/` works this way.

- When a skill is given a `slice.md`, that slice's `issues/` folder is where its tickets are published and fetched.
- A slice is unblocked when every slice in its `Blocked by` line is merged into `main`.
- A ticket is closed by setting `Status: done` in the same commit that implements it. A slice is closed by setting `Status: done` in its `slice.md` in the slice's last commit.

## When a skill says "publish to the issue tracker"

Create a new file under `.scratch/<feature-slug>/` (creating the directory if needed).

## When a skill says "fetch the relevant ticket"

Read the file at the referenced path. The user will normally pass the path or the issue number directly.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a file with one **child** file per ticket.

The tracker is shared through the git branch, and other sessions edit it at the same time. The local checkout may be stale, so:

- **Sync before reading the map**: `git fetch origin <branch>` and fast-forward (`git pull --ff-only origin <branch>`) before loading `map.md` or scanning the frontier.
- **Claim with a push**: commit the `Status: claimed` change and push it at once, before any other work. A claim only counts once it is pushed.
- **Rejected push = someone got there first**: fetch, and if the remote already claimed or resolved the ticket, drop your local claim commit (`git reset --hard origin/<branch>` while it's your only unpushed commit) and pick again from the fresh frontier.
- **Fetch again before pushing a resolution**, and merge if the remote moved.

- **Map**: `.scratch/<effort>/map.md` (the Notes / Decisions-so-far / Fog body).
- **Child ticket**: `.scratch/<effort>/issues/NN-<slug>.md`, numbered from `01`, with the question in the body. A `Type:` line records the ticket type (`research`/`prototype`/`grilling`/`task`); a `Status:` line records `claimed`/`resolved`.
- **Blocking**: a `Blocked by: NN, NN` line near the top. A ticket is unblocked when every file it lists is `resolved`.
- **Frontier**: scan `.scratch/<effort>/issues/` for files that are open, unblocked, and unclaimed; first by number wins.
- **Claim**: set `Status: claimed` and save before any work.
- **Resolve**: append the answer under an `## Answer` heading, set `Status: resolved`, then append a context pointer (gist + link) to the map's Decisions-so-far in `map.md`.
