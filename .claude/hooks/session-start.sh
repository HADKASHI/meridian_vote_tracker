#!/bin/bash
# Prepares Claude Code cloud sessions to build and test the stack, so every
# parallel slice session starts ready. Does nothing on a developer machine.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-.}"

# .NET 10 SDK, Node 24 and the Docker daemon. Idempotent: installs are skipped
# once done, and the daemon is started only when it isn't running. Its output
# goes to a log; only the versions reach the session.
log=/tmp/cloud-dev-setup.log
if ! .claude/cloud-dev-setup.sh >"$log" 2>&1; then
  tail -20 "$log" >&2
  exit 1
fi
echo "Dev setup ready: $(tail -3 "$log" | paste -sd' ' -) (log: $log)"

# Node 24 first on PATH for the rest of the session (Angular 22 refuses the
# preinstalled Node 22).
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  echo 'export PATH=/opt/node24/bin:$PATH' >> "$CLAUDE_ENV_FILE"
fi
