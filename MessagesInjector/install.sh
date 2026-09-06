#!/bin/bash
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
APP_NAME="Messages-patched.app"
APP_PATH="$DIR/$APP_NAME"
ENTITLEMENTS="$DIR/entitlements.plist"
DYLIB="$DIR/libMessagesInjector.dylib"
MOUNT_POINT="/tmp/gamepigeonfucker-pristine"

log() { printf '==> %s\n' "$1"; }
fail() { printf 'ERROR: %s\n' "$1" >&2; exit 1; }

require_macos() {
    [ "$(uname -s)" = "Darwin" ] || fail "this only runs on macOS"
}

check_prereqs() {
    log "Checking prerequisites"

    command -v clang >/dev/null 2>&1 || fail "clang not found - run 'xcode-select --install'"
    command -v codesign >/dev/null 2>&1 || fail "codesign not found - run 'xcode-select --install'"
    command -v python3 >/dev/null 2>&1 || fail "python3 not found"
    command -v sqlite3 >/dev/null 2>&1 || fail "sqlite3 not found"
    python3 -c "import lief" >/dev/null 2>&1 || fail "python module 'lief' not found - run 'pip3 install --user lief'"

    id -Gn | tr ' ' '\n' | grep -qx admin || fail "current user is not in the admin group - admin/sudo access is required to mount the APFS snapshot"

    [ -d "$HOME/Library/Messages" ] || fail "~/Library/Messages does not exist - sign into iMessage in the regular Messages app at least once before running this"
}

check_full_disk_access() {
    log "Checking Full Disk Access"

    local chat_db="$HOME/Library/Messages/chat.db"
    [ -f "$chat_db" ] || fail "$chat_db not found - sign into iMessage in the regular Messages app first"

    if ! sqlite3 "$chat_db" "select 1 from chat limit 1" >/dev/null 2>&1; then
        fail "cannot read $chat_db - grant Full Disk Access to your terminal app in System Settings > Privacy & Security > Full Disk Access, then restart the terminal and re-run this script"
    fi
}

build_dylib() {
    log "Building $DYLIB"
    make -C "$DIR" >/dev/null
    [ -f "$DYLIB" ] || fail "build finished but $DYLIB is missing"
}

find_snapshot() {
    local snapshot
    snapshot=$(diskutil apfs listSnapshots / 2>/dev/null \
        | grep -oE 'com\.apple\.os\.update-[A-Za-z0-9.-]+' \
        | sort -u | tail -1)
    [ -n "$snapshot" ] || fail "no com.apple.os.update-* APFS snapshot found on / - run a macOS software update first"
    printf '%s' "$snapshot"
}

find_system_volume_device() {
    local device
    device=$(diskutil apfs list -plist | plutil -convert json -o - - | python3 -c '
import json, sys
data = json.load(sys.stdin)
for container in data.get("Containers", []):
    for vol in container.get("Volumes", []):
        if "System" in vol.get("Roles", []):
            print(vol["DeviceIdentifier"])
            sys.exit(0)
')
    [ -n "$device" ] || fail "could not find the System volume's device via diskutil apfs list"
    printf '%s' "$device"
}

copy_pristine_app() {
    if [ -d "$APP_PATH" ]; then
        log "$APP_NAME already exists, skipping copy (delete it to force a fresh copy)"
        return
    fi

    log "Finding APFS snapshot"
    local snapshot device
    snapshot=$(find_snapshot)
    device=$(find_system_volume_device)
    log "Using snapshot $snapshot on /dev/$device"

    mkdir -p "$MOUNT_POINT"
    sudo mount_apfs -o ro,nobrowse -s "$snapshot" "/dev/$device" "$MOUNT_POINT"

    log "Copying pristine Messages.app"
    cp -R "$MOUNT_POINT/System/Applications/Messages.app" "$APP_PATH"

    sudo umount "$MOUNT_POINT"
    rmdir "$MOUNT_POINT"
}

patch_entitlements() {
    log "Extracting entitlements"
    codesign -d --entitlements "$ENTITLEMENTS" --xml "$APP_PATH/Contents/MacOS/Messages"

    /usr/libexec/PlistBuddy -c 'Add :com.apple.security.cs.disable-library-validation bool true' "$ENTITLEMENTS" 2>/dev/null \
        || /usr/libexec/PlistBuddy -c 'Set :com.apple.security.cs.disable-library-validation true' "$ENTITLEMENTS"
    /usr/libexec/PlistBuddy -c 'Set :com.apple.security.app-sandbox false' "$ENTITLEMENTS"

    local disable_lv sandbox
    disable_lv=$(/usr/libexec/PlistBuddy -c 'Print :com.apple.security.cs.disable-library-validation' "$ENTITLEMENTS")
    sandbox=$(/usr/libexec/PlistBuddy -c 'Print :com.apple.security.app-sandbox' "$ENTITLEMENTS")
    [ "$disable_lv" = "true" ] || fail "failed to set com.apple.security.cs.disable-library-validation"
    [ "$sandbox" = "false" ] || fail "failed to clear com.apple.security.app-sandbox"
}

patch_dylib_and_sign() {
    log "Patching $DYLIB into the Messages binary"
    python3 - "$APP_PATH/Contents/MacOS/Messages" "$DYLIB" <<'PYEOF'
import lief
import sys

binary_path, dylib_path = sys.argv[1], sys.argv[2]
fat = lief.MachO.parse(binary_path)
for binary in fat:
    binary.add_library(dylib_path)
fat.write(binary_path)
PYEOF

    log "Re-signing $APP_NAME"
    codesign --force --sign - --identifier com.apple.MobileSMS \
        --entitlements "$ENTITLEMENTS" "$APP_PATH"
}

main() {
    require_macos
    check_prereqs
    check_full_disk_access
    build_dylib
    copy_pristine_app
    patch_entitlements
    patch_dylib_and_sign
    "$DIR/sideload.sh"
}

main "$@"
