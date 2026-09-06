#!/bin/bash
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
APP_PATH="$DIR/Messages-patched.app"
SOCKET_PATH="/tmp/gamepigeonfucker-injector.sock"

log() { printf '==> %s\n' "$1"; }
fail() { printf 'ERROR: %s\n' "$1" >&2; exit 1; }

[ -d "$APP_PATH" ] || fail "$APP_PATH not found - run install.sh first"

log "Killing any running Messages"
killall Messages >/dev/null 2>&1 || true
sleep 1
rm -f "$SOCKET_PATH"

log "Launching $(basename "$APP_PATH")"
open "$APP_PATH"

log "Waiting for $SOCKET_PATH"
for _ in $(seq 1 20); do
    [ -S "$SOCKET_PATH" ] && break
    sleep 1
done
[ -S "$SOCKET_PATH" ] || fail "$SOCKET_PATH never appeared - check Console.app for a crash log"

log "Ready: $SOCKET_PATH"
