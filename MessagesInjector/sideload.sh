#!/bin/sh
DIR="$(cd "$(dirname "$0")" && pwd)"
killall Messages 2>/dev/null
sleep 1
rm -f /tmp/gamepigeonfucker-injector.sock

nohup env DYLD_INSERT_LIBRARIES="$DIR/libMessagesInjector.dylib" "$DIR/Messages-unsigned.app/Contents/MacOS/Messages" > /tmp/msg_unsigned.log 2>&1 < /dev/null &
disown

sleep 3
ls -la /tmp/gamepigeonfucker-injector.sock
