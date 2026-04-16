#!/usr/bin/env bash
set -euo pipefail

detect_ip() {
  local iface ip
  for iface in en0 en1; do
    ip="$(ipconfig getifaddr "$iface" 2>/dev/null || true)"
    if [[ -n "$ip" ]]; then
      echo "$ip"
      return 0
    fi
  done

  if command -v ifconfig >/dev/null 2>&1; then
    ip="$(
      ifconfig 2>/dev/null | awk '
        /^[a-z0-9]/ { iface=$1; sub(":", "", iface) }
        /status: active/ { active[iface]=1 }
        /inet / && $2 != "127.0.0.1" { ip[iface]=$2 }
        END {
          if (active["en0"] && ip["en0"] != "") { print ip["en0"]; exit }
          if (active["en1"] && ip["en1"] != "") { print ip["en1"]; exit }
          for (name in ip) {
            if (active[name]) { print ip[name]; exit }
          }
        }
      '
    )"
    if [[ -n "$ip" ]]; then
      echo "$ip"
      return 0
    fi
  fi

  if command -v hostname >/dev/null 2>&1; then
    ip="$(hostname -I 2>/dev/null | awk '{print $1}')"
    if [[ -n "$ip" ]]; then
      echo "$ip"
      return 0
    fi
  fi

  return 1
}

if ip="$(detect_ip)"; then
  echo "http://$ip:8080/"
  exit 0
fi

echo "Unable to detect a LAN IP automatically. Insert your Mac IP manually." >&2
exit 1
