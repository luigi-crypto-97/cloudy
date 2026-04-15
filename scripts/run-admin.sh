#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if lsof -nP -iTCP:8090 -sTCP:LISTEN >/dev/null 2>&1; then
  if curl -fsS -I http://127.0.0.1:8090 >/dev/null 2>&1; then
    echo "FriendMap admin already appears to be running at http://localhost:8090"
    exit 0
  fi

  echo "Port 8090 is already in use by another process:" >&2
  lsof -nP -iTCP:8090 -sTCP:LISTEN >&2
  echo >&2
  echo "Stop that process or run the admin on another port." >&2
  exit 1
fi

ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/FriendMap.Admin/FriendMap.Admin.csproj
