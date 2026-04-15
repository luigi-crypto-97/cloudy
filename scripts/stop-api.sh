#!/usr/bin/env bash
set -euo pipefail

if pgrep -f "FriendMap.Api|src/FriendMap.Api/FriendMap.Api.csproj" >/dev/null 2>&1; then
  pkill -f "FriendMap.Api|src/FriendMap.Api/FriendMap.Api.csproj"
  echo "FriendMap API stopped."
else
  echo "FriendMap API is not running."
fi
