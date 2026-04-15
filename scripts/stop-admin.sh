#!/usr/bin/env bash
set -euo pipefail

if pgrep -f "FriendMap.Admin|src/FriendMap.Admin/FriendMap.Admin.csproj" >/dev/null 2>&1; then
  pkill -f "FriendMap.Admin|src/FriendMap.Admin/FriendMap.Admin.csproj"
  echo "FriendMap admin stopped."
else
  echo "FriendMap admin is not running."
fi
