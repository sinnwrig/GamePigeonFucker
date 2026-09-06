#!/bin/sh
DIR="$(cd "$(dirname "$0")" && pwd)"
killall Messages 2>/dev/null
sleep 1
DYLD_INSERT_LIBRARIES="$DIR/libMessagesInjector.dylib" /System/Applications/Messages.app/Contents/MacOS/Messages &
