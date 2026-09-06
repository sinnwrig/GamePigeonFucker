# MessagesInjector

## Description

`libMessagesInjector.dylib` is loaded into a copy of `Messages.app` to expose
a local control socket for driving iMessage's private `IMCore` send path
directly. This is what lets GamePigeonFucker send messages with a custom
`balloonBundleID`/`payloadData` (GamePigeon payloads) - something
AppleScript's `send` command can't set - and lets it choose which of the
Apple ID's registered addresses (a phone number vs an iCloud email alias) a
message is sent from, which the stock Messages UI doesn't expose per-message
either.

It runs inside a patched copy of the real `Messages.app`, not a standalone
binary - so it shares the Mac's real `~/Library/Messages` state, Keychain
identity, and iMessage account registration. Building/patching the copy
never touches `/System` and doesn't require SIP, SSV, or FileVault changes
(see below) - but SIP must be **disabled** to actually launch the patched
app. It's ad-hoc signed and claims Apple's own `com.apple.MobileSMS`
bundle identifier while carrying an injected dylib; with SIP enabled,
`spctl`/RunningBoard rejects it and `open`/`launchd` fails with
`RBSRequestErrorDomain Code=5` (`Launchd job spawn failed`,
`NSPOSIXErrorDomain Code=163`). Confirmed by re-enabling SIP (and SSV) on
a testbench Mac: `dotnet` then worked fine, but `Messages-patched.app`
immediately failed to launch again until SIP was turned back off.

## API

Connect to `/tmp/gamepigeonfucker-injector.sock` (Unix domain,
`SOCK_STREAM`). Each request and response is a 4-byte big-endian length
prefix followed by a UTF-8 JSON payload.

### Send a message - `{}`  (no `cmd` field)

```json
{
  "chatGuid": "any;-;+15305550143",
  "text": "hello",
  "balloonBundleId": null,
  "payloadDataBase64": null
}
```

- `chatGuid` (required) - must be an **existing** chat GUID (look it up in
  `chat.db`'s `chat` table). This command only attaches to existing chats,
  it doesn't create new ones - use `sendViaAccount` for that.
- `text` - plain text body. Internally wrapped in an `NSAttributedString`
  before being handed to IMCore.
- `balloonBundleId` / `payloadDataBase64` - set both to send a GamePigeon
  (or other iMessage app extension) payload instead of plain text.
  `payloadDataBase64` is the base64 of the raw `NSData` payload.
- `senderHandle` - **do not use.** It's wired straight into IMCore's
  `sender:` parameter, which expects a real `IMHandle` object, not a
  string - passing anything here crashes the whole Messages process
  (`-isLoginIMHandle: unrecognized selector`). To control which identity a
  message is sent from, use `sendViaAccount` below instead.

Response: `{"ok": true}` or `{"ok": false, "error": "..."}`.

### `listAccounts`

```json
{ "cmd": "listAccounts" }
```

Returns every registered `IMAccount` on the Mac, each with its `uniqueID`,
`loginIMHandle`, and `aliases`. Use this to find the `accountUniqueID` to
pass to `sendViaAccount`.

Only trust an account whose `aliases` list is non-empty as a real,
send-capable iMessage identity. An account with **empty** `aliases` is a
carrier SMS/RCS relay pseudo-account (used for cellular relay via a paired
iPhone) - sending through one of those fails with `service: SMS` and a
nonzero `error` in `chat.db` unless a real relay connection exists.

Response: `{"ok": true, "accounts": [ {"class", "uniqueID", "loginIMHandle", "aliases", ...}, ... ]}`.

### `sendViaAccount`

```json
{
  "cmd": "sendViaAccount",
  "accountUniqueID": "C536C211-5F65-4C96-AADE-251674715BBF",
  "recipientHandleID": "+15305550143",
  "senderIdentityID": "+15304089143",
  "text": "hello"
}
```

Sends plain text through a specific account, optionally forcing which of
that account's own aliases the message is sent *from*.

- `accountUniqueID` (required) - from `listAccounts`. Pick one with a
  non-empty `aliases` list.
- `recipientHandleID` (required) - the recipient's address (phone or
  email). Resolved via `[account imHandleWithID:]`, then
  `[IMChatRegistry chatForIMHandle:]` gets or creates the chat.
- `senderIdentityID` (optional) - one of the account's own `aliases`. If
  set, calls `[chat setLastAddressedHandleID:]` before sending, which is
  what actually controls the outgoing "from" identity - it's sticky per
  chat, so a chat that's ever been addressed from one alias keeps using it
  until explicitly changed this way. If omitted, the chat's existing
  (or default) addressed identity is used unchanged.

Verify which identity a send actually used via `chat.db`, not just the
`ok: true` response - query `destination_caller_id` on the `message` table
(not the `account` column, which just reflects which local account object
owns the chat and doesn't change):

```
sqlite3 ~/Library/Messages/chat.db \
  "SELECT ROWID, account, destination_caller_id, service, error, is_delivered \
   FROM message WHERE is_from_me=1 ORDER BY ROWID DESC LIMIT 5;"
```

Response: `{"ok": true, "chatDescription": "<IMChat ...>"}` or
`{"ok": false, "error": "..."}`.

### `introspect`

```json
{ "cmd": "introspect" }
```

Debug helper - dumps filtered Objective-C method lists from `IMChatRegistry`,
`IMAccount`, and `IMChat` (methods whose names contain `chat`/`account`,
`handle`, or `lastaddressed`/`setaccount` respectively). Useful for finding
private API surface directly from the live, running IMCore instance rather
than guessing from documentation of older macOS versions, since Apple's
private API shapes change between OS releases. Not needed for normal use.

## Installation on machine

Building the patched copy (steps below) never touches `/System` and
doesn't require disabling SIP, SSV, or FileVault - everything happens on
a writable copy of the app you make yourself. **Launching the result
does** - SIP must be disabled first, or `open Messages-patched.app` /
`sideload.sh` fails with `RBSRequestErrorDomain Code=5` because the
ad-hoc-signed, dylib-injected app is impersonating a system bundle
identifier (`com.apple.MobileSMS`) that Gatekeeper/RunningBoard rejects
once SIP is enforcing normally. SSV and FileVault do not need to be
disabled for either step.

**Prerequisites:**
- Xcode Command Line Tools (`xcode-select --install`) for `clang` and
  `codesign`.
- `pip3 install --user lief` - for patching in the dylib's load command.
  Do not use `insert_dylib` (Tyilo) - it doesn't update the
  `LC_DYLD_CHAINED_FIXUPS` table when it shifts load commands, which
  corrupts modern signed binaries (crashes with `EXC_BAD_INSTRUCTION` at a
  tiny offset into the binary's own entry point, before any injected code
  runs).
- Admin/sudo access, to mount an APFS snapshot read-only. This does **not**
  require SIP/SSV disabled - it's a normal, always-available APFS
  operation (the same thing Time Machine local snapshots use).
- The Mac must already be signed into iMessage in the regular Messages app
  at least once, so `~/Library/Messages` and the account Keychain state
  exist. The patched copy reuses that state; it doesn't register a new
  device or account.

**1. Copy a pristine `Messages.app` out of the system directory.** Don't
copy the currently-installed one if it might already be modified - pull a
known-clean copy from an APFS snapshot instead:

```
diskutil apfs listSnapshots /
```

Look for a `com.apple.os.update-*` snapshot (from the last software
update). Ignore any `com.apple.bless.*` snapshots. Find the System
volume's device from `diskutil apfs list` (Role `(System)`, e.g.
`disk3s1`), then:

```
mkdir /tmp/pristine
sudo mount_apfs -o ro,nobrowse -s 'com.apple.os.update-<snapshot-name>' /dev/disk3s1 /tmp/pristine
cp -R /tmp/pristine/System/Applications/Messages.app ./Messages-patched.app
sudo umount /tmp/pristine
```

Copy the whole app bundle, not just the executable - it needs its
`Info.plist`, `Resources`, and `PlugIns` intact.

**2. Extract and edit entitlements.**

```
codesign -d --entitlements /tmp/entitlements.plist --xml \
    ./Messages-patched.app/Contents/MacOS/Messages
```

Keep every entitlement Apple shipped - in particular
`application-identifier` (`com.apple.MobileSMS`), which is what lets the
copy register with `imagent`/Keychain/`chat.db` as if it were the real app.
Change exactly two:

```
/usr/libexec/PlistBuddy -c 'Add :com.apple.security.cs.disable-library-validation bool true' /tmp/entitlements.plist
/usr/libexec/PlistBuddy -c 'Set :com.apple.security.app-sandbox false' /tmp/entitlements.plist
```

- `com.apple.security.cs.disable-library-validation` -> `true`: without
  this, dyld silently refuses to load `libMessagesInjector.dylib` because
  it isn't signed by the same team as the (now ad-hoc-signed) host binary -
  the app just runs with no socket and no visible error.
- `com.apple.security.app-sandbox` -> `false`: the stock entitlements ship
  with App Sandbox on, which silently blocks the injector's Unix-domain
  socket bind under `/tmp` - again, no error, the socket just never
  appears.

Don't strip anything else - removing entitlements you don't understand is
more likely to break account registration than help.

**3. Patch in the dylib and re-sign.**

```python
import lief
fat = lief.MachO.parse("Messages-patched.app/Contents/MacOS/Messages")
for binary in fat:
    binary.add_library("/absolute/path/to/libMessagesInjector.dylib")
fat.write("Messages-patched.app/Contents/MacOS/Messages")
```

```
codesign --force --sign - --identifier com.apple.MobileSMS \
    --entitlements /tmp/entitlements.plist Messages-patched.app
```

**4. Run it.**

```
./sideload.sh
```

Kills any running Messages, launches `Messages-patched.app` directly (not
`/System/Applications/Messages.app`), and waits for
`/tmp/gamepigeonfucker-injector.sock` to appear. Because it shares
`~/Library/Messages` and Keychain state with the real app, it comes up
already signed in with full chat history - no separate login step.

## Known gotchas hit getting here

- `initWithSender:...` on newer macOS takes a trailing `threadIdentifier:`
  argument that isn't in older documented signatures. Get the real
  selector and argument types from the live binary with
  `class_copyMethodList`/`method_getTypeEncoding` rather than guessing.
- The `error:` parameter of that initializer is typed `id`, not
  `NSError **`, despite the name. Passing `&someNSError` crashes
  (`objc_retain` on a stack address). Pass `nil`.
- `text:` must be an `NSAttributedString`, not a plain `NSString` - IMCore
  calls `-string` on it internally.
- `[chat sendMessage:]` must be called on the main dispatch queue. Calling
  it from a background thread hits a `dispatch_assert_queue` failure and
  crashes the process - often *after* the message has actually already
  been dispatched, making it look like it worked right up until the
  crash. Wrap it in `dispatch_sync(dispatch_get_main_queue(), ^{ ... })`.
- The `sender:` parameter of `initWithSender:...` expects a real `IMHandle`
  object (it's checked via `-isLoginIMHandle`), not a string - see the
  `senderHandle` warning in the API section above.
- Which of an account's aliases a message is sent *from* is controlled by
  `IMChat`'s `lastAddressedHandleID`, which is sticky per chat once set -
  not by anything passed to `chatForIMHandle:...` at lookup time, and not
  by `IMMessage`'s `sender:` parameter.
