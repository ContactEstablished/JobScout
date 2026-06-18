#!/usr/bin/env bash
# JobScout launcher — Unix
# Requires .NET 10 SDK. Boots the API (which hosts the Blazor UI) and opens a browser.

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "❌ .NET is not installed."
  echo "   Download .NET 10 from https://dotnet.microsoft.com/download and try again."
  exit 1
fi

SDK_MAJOR=$(dotnet --list-sdks | awk '{print $1}' | sort -V | tail -n1 | cut -d. -f1)
if [ -z "$SDK_MAJOR" ] || [ "$SDK_MAJOR" -lt 10 ]; then
  echo "❌ .NET 10 SDK or newer is required (found: $(dotnet --list-sdks | tail -n1))."
  echo "   Download .NET 10 from https://dotnet.microsoft.com/download and try again."
  exit 1
fi

echo "▶ Restoring dependencies…"
dotnet restore >/dev/null

echo "▶ Building…"
dotnet build --no-restore --nologo

URL="${JOBSCOUT_URL:-http://localhost:5000}"
echo "▶ Starting JobScout at $URL"

# Open browser after a short delay, in the background.
(
  sleep 3
  if command -v xdg-open >/dev/null 2>&1; then xdg-open "$URL" >/dev/null 2>&1
  elif command -v open >/dev/null 2>&1; then open "$URL"
  fi
) &

trap 'echo; echo "▶ Stopping JobScout"; kill 0' INT TERM
dotnet run --no-build --project src/JobScout.Api/JobScout.Api.csproj --launch-profile JobScout
