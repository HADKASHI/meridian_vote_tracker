#!/usr/bin/env bash
# Prepares a Claude Code cloud session to build and run the stack.
# Idempotent: safe to run at the start of every session. Not needed on a
# normal developer machine with Docker, the .NET 10 SDK and Node 24.
#
# Afterwards, prefix commands with: export PATH=/opt/node24/bin:$PATH
set -euo pipefail

# .NET 10 SDK. dot.net and builds.dotnet.microsoft.com are blocked by the
# proxy; Ubuntu 24.04 ships the SDK in its own archive.
if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q '^10\.'; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0
fi

# Node 24 LTS. The preinstalled Node 22.22.0 is below Angular 22's minimum
# (^22.22.3 || ^24.15.0).
if [ ! -x /opt/node24/bin/node ]; then
  version=$(curl -sS https://nodejs.org/dist/index.json |
    python3 -c "import json,sys;print(next(r['version'] for r in json.load(sys.stdin) if r['version'].startswith('v24.')))")
  tmp=$(mktemp -d)
  curl -sSL -o "$tmp/node.tar.xz" "https://nodejs.org/dist/$version/node-$version-linux-x64.tar.xz"
  mkdir -p /opt/node24
  tar -xJf "$tmp/node.tar.xz" -C /opt/node24 --strip-components=1
  rm -rf "$tmp"
fi

# Docker daemon.
if ! docker info >/dev/null 2>&1; then
  (dockerd >/tmp/dockerd.log 2>&1 &)
  for _ in $(seq 1 30); do docker info >/dev/null 2>&1 && break; sleep 1; done
fi

dotnet --version
/opt/node24/bin/node --version
docker info --format 'Docker {{.ServerVersion}}'
