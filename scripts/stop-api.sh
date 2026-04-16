#!/usr/bin/env bash
set -euo pipefail

PORT_PIDS="$(lsof -tiTCP:8080 -sTCP:LISTEN 2>/dev/null | sort -u | tr '\n' ' ' | xargs || true)"
MATCH_PATTERN="FriendMap.Api|src/FriendMap.Api/FriendMap.Api.csproj|dotnet .*FriendMap.Api|FriendMap "

if [[ -n "$PORT_PIDS" ]]; then
  kill $PORT_PIDS
  echo "FriendMap API stopped on port 8080."
elif pgrep -f "$MATCH_PATTERN" >/dev/null 2>&1; then
  pkill -f "$MATCH_PATTERN"
  echo "FriendMap API stopped."
else
  echo "FriendMap API is not running."
fi
