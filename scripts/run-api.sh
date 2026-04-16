#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

DESIRED_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:8080}"

if lsof -nP -iTCP:8080 -sTCP:LISTEN >/dev/null 2>&1; then
  LISTENERS="$(lsof -nP -iTCP:8080 -sTCP:LISTEN || true)"

  if [[ "$DESIRED_URLS" == *"0.0.0.0:8080"* ]] && echo "$LISTENERS" | grep -q "127.0.0.1:8080"; then
    echo "Port 8080 is already used by a localhost-only instance of the API." >&2
    echo "Stop it first, then restart in LAN mode:" >&2
    echo "  ./scripts/stop-api.sh" >&2
    echo "  ./scripts/run-api-lan.sh" >&2
    exit 1
  fi

  if curl -fsS http://127.0.0.1:8080/health >/dev/null 2>&1; then
    if [[ "$DESIRED_URLS" == *"0.0.0.0:8080"* ]]; then
      echo "FriendMap API already appears to be running and reachable on port 8080."
    else
      echo "FriendMap API already appears to be running at http://localhost:8080"
    fi
    exit 0
  fi

  echo "Port 8080 is already in use by another process:" >&2
  echo "$LISTENERS" >&2
  echo >&2
  echo "Stop that process or run the API on another port." >&2
  exit 1
fi

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="$DESIRED_URLS" \
  dotnet run --no-launch-profile --project src/FriendMap.Api/FriendMap.Api.csproj
