#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if lsof -nP -iTCP:8080 -sTCP:LISTEN >/dev/null 2>&1; then
  if curl -fsS http://127.0.0.1:8080/health >/dev/null 2>&1; then
    echo "FriendMap API already appears to be running at http://localhost:8080"
    exit 0
  fi

  echo "Port 8080 is already in use by another process:" >&2
  lsof -nP -iTCP:8080 -sTCP:LISTEN >&2
  echo >&2
  echo "Stop that process or run the API on another port." >&2
  exit 1
fi

ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/FriendMap.Api/FriendMap.Api.csproj
