#!/usr/bin/env bash
# The guided live check of the Azure services (ADR 0009, docs/azure.md). Run it on your own machine:
#   scripts/check-azure.sh [path/to/a/real/voting/sheet.pdf]
# It reads the .env beside this repository, checks Blob upload and download, the database and its version,
# one Document Intelligence call on a real sheet and Application Insights, and prints what passed and what did not.
# It writes nothing, and sends nothing but what the checks themselves send to your own Azure resources.
# Needs the .NET SDK (see global.json). With a connection string without a Password, a BLOB_SERVICE_URI, or a
# DOCINTEL_ENDPOINT without a key, the check signs in with Microsoft Entra ID: run `az login` first. The sheet stays where it is: it is never copied into the repository.
set -euo pipefail
cd "$(dirname "$0")/.."

sheet="${1:-}"
if [[ ! -f .env ]]; then
  echo "No .env here. Copy .env.example to .env and fill in the Azure values (docs/azure.md), then run this again."
  exit 2
fi
if [[ -z "$sheet" ]]; then
  echo "Document Intelligence is checked by reading one real voting sheet (a PDF), which stays where it is."
  read -r -p "Path to a real sheet, or press Enter to skip that check: " sheet
fi

args=(--env .env)
if [[ -n "$sheet" ]]; then
  [[ -f "$sheet" ]] || { echo "No such file: $sheet"; exit 2; }
  args+=(--sheet "$sheet")
fi

echo
exec dotnet run --project src/Meridian.AzureCheck -c Release -- "${args[@]}"
