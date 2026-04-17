#!/usr/bin/env bash
set -euo pipefail

DEVICE_ID="${1:-00008110-000538893C0A801E}"
APP_ID="${APP_ID:-it.luiginegri.FriendMapSeed}"
PROJECT="src/FriendMap.Mobile/FriendMap.Mobile.csproj"

echo "Checking paired iOS devices..."
xcrun devicectl list devices || true

cat <<EOF

Preflight:
- keep the iPhone unlocked
- keep the screen on
- leave the device connected via USB

If launch fails with 'Locked', the build is fine: SpringBoard denied opening the app because the phone was locked.
EOF

dotnet restore "$PROJECT" -r ios-arm64
dotnet build -t:Run "$PROJECT" \
  -f net8.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:_DeviceName="$DEVICE_ID" \
  -p:ApplicationId="$APP_ID"
