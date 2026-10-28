#!/usr/bin/env bash
# Sandbox only (Claude Code cloud sessions): builds local images that shadow the base
# images whose build steps download packages, with the session proxy's CA trusted, so the
# committed Dockerfiles build here unchanged. Idempotent; run once per session after
# cloud-dev-setup.sh; compose.sandbox.yaml says how to build and start the stack.
set -euo pipefail
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
cp /root/.ccr/ca-bundle.crt "$tmp/sandbox-ca.crt"

# Docker Hub often answers this sandbox with 429; its official images are also on mirror.gcr.io.
pull() {
  local image=$1
  docker image inspect "$image" >/dev/null 2>&1 && return
  docker pull -q "$image" >/dev/null 2>&1 && return
  case "$image" in
    mcr.microsoft.com/*) docker pull -q "$image" >/dev/null ;;
    */*) docker pull -q "mirror.gcr.io/$image" >/dev/null && docker tag "mirror.gcr.io/$image" "$image" ;;
    *) docker pull -q "mirror.gcr.io/library/$image" >/dev/null && docker tag "mirror.gcr.io/library/$image" "$image" ;;
  esac
}

shadow() { # image, trust command
  local image=$1 trust=$2
  if [ "$(docker image inspect -f '{{index .Config.Labels "sandbox.ca"}}' "$image" 2>/dev/null)" = "1" ]; then
    return
  fi
  pull "$image"
  printf 'FROM %s\nCOPY sandbox-ca.crt /usr/local/share/ca-certificates/sandbox-ca.crt\nRUN %s\nENV NODE_EXTRA_CA_CERTS=/usr/local/share/ca-certificates/sandbox-ca.crt\nLABEL sandbox.ca=1\n' \
    "$image" "$trust" > "$tmp/Dockerfile"
  docker build -q -t "$image" "$tmp" >/dev/null
  echo "Shadowed $image with the sandbox CA."
}

shadow mcr.microsoft.com/dotnet/sdk:10.0 update-ca-certificates
shadow mcr.microsoft.com/dotnet/aspnet:10.0 update-ca-certificates
shadow node:24-alpine 'cat /usr/local/share/ca-certificates/sandbox-ca.crt >> /etc/ssl/certs/ca-certificates.crt'

# Runtime images with no package downloads only need to be present.
pull nginx:1.30-alpine
pull postgres:18
