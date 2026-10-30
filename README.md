# meridian_vote_tracker

> The full README is written later, in the finishing slice (`.scratch/meridian-build/slices/08-finishing-pass/slice.md`). Until then, two notes.

## Before you run it

`compose.yaml` mounts `DATA_DIR` (default `./candidate_data`) into the migrate service. In a fresh clone that folder holds only its README, because the assignment data is gitignored (see `.gitignore` and `candidate_data/README.md`). Before `docker compose up`, put `member_states.csv` in `candidate_data/`, or set `DATA_DIR` in `.env` (copy `.env.example`) to a folder that contains it, for example `tests/data` for a demo set. Without that file the migrate step fails and the stack does not start.

## Identity (demo only)

Authentication is a plain `X-User-Id` header, handled by `src/Meridian.Platform/Identity/UserHeaderHandler.cs`. It is unauthenticated: anyone who knows a user id acts as that user, including an Admin. This is a deliberate demo shortcut and must not be used in production. The replacement is real authentication (for example Entra ID with JWT bearer) behind the same policies in `src/Meridian.Platform/Identity/Policies.cs`.