#!/usr/bin/env bash
# The guided live check of the Azure services (ADR 0009, docs/azure.md). Run it on your own machine:
#   scripts/check-azure.sh [path/to/a/real/voting/sheet.pdf] [--device-code]
# It reads the .env beside this repository, checks Blob upload and download and one Document Intelligence call on a
# real sheet, and prints what passed and what did not. (The database and Application Insights are not checked live.)
# It writes nothing, and sends nothing but what the checks themselves send to your own Azure resources.
# Needs the .NET SDK (see global.json). With a connection string without a Password, a BLOB_SERVICE_URI, or a
# DOCINTEL_ENDPOINT without a key, the check signs in with Microsoft Entra ID: run `az login` first. The sheet stays where it is: it is never copied into the repository.
set -euo pipefail
cd "$(dirname "$0")/.."

# Flags (such as --device-code, to sign in to Entra ID with a code when there is no `az login`) go to the check;
# the one other argument is the sheet.
sheet=""
flags=()
for argument in "$@"; do
  if [[ "$argument" == --* ]]; then flags+=("$argument"); else sheet="$argument"; fi
done
if [[ ! -f .env ]]; then
  echo "No .env here. Copy .env.example to .env and fill in the Azure values (docs/azure.md), then run this again."
  exit 2
fi
if [[ -z "$sheet" ]]; then
  echo "Document Intelligence is checked by reading one real voting sheet (a PDF), which stays where it is."
  read -r -p "Path to a real sheet, or press Enter to skip that check: " sheet || sheet=""
fi

args=(--env .env)
if [[ -n "$sheet" ]]; then
  [[ -f "$sheet" ]] || { echo "No such file: $sheet"; exit 2; }
  args+=(--sheet "$sheet")
fi

echo
exec dotnet run --project src/Meridian.AzureCheck -c Release -- "${args[@]}" "${flags[@]}"
