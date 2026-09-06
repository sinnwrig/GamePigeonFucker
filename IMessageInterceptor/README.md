`IMessageInterceptor` is a .NET class library for watching and sending
iMessages on macOS. It polls `~/Library/Messages/chat.db` (needs Full Disk
Access) for new rows and sends through `MessagesInjector`'s Unix socket
(`../MessagesInjector/`), a patched copy of `Messages.app` that exposes
IMCore's private send path -- which is what lets it set a GamePigeon
`balloonBundleId`/`payloadData` and choose which registered account/alias a
message is sent from, neither of which AppleScript's `send` command can do.

Target framework: net10.0. Package dependency: `Microsoft.Data.Sqlite`.

## Core types

- `IMessageTransport` -- the abstraction: `WatchAsync`, `SendAsync`,
  `ListAccountsAsync`. `ChatDatabaseTransport` is the only implementation:
  it reads `chat.db` for inbound messages and calls `InjectorClient`
  (talks to `/tmp/gamepigeonfucker-injector.sock`) for outbound sends and
  account listing.
- `MessagingService` -- thin wrapper around a transport. Exposes
  `OnReceiveMessage`, `StartAsync(cancellationToken)`,
  `SendMessageAsync(chatIdentifier, OutboundMessage)`,
  `ListAccountsAsync()`. This is the type application code should hold onto.
- `InboundMessage` (record) -- `Guid`, `ChatIdentifier`, `HandleId`, `Text`,
  `BalloonBundleId`, `PayloadData` (raw `payload_data` bytes, null if none),
  `Timestamp`, `IsFromMe`.
- `OutboundMessage` (record) -- `Text`, `RawPayload`, `BalloonBundleId`,
  `SenderAccountUniqueId`, `SenderIdentityId`. Leave the last three null for
  a plain-text send through the default account.
- `ImAccountInfo` (record) -- `ClassName`, `UniqueId`, `LoginImHandle`,
  `Aliases`, `LoginHandles`, as returned by `listAccounts` on the injector.
  Only an account with a non-empty `Aliases` is a real send-capable
  iMessage identity (see `MessagesInjector/README.md`).

## Basic usage

```csharp
using IMessage;

var transport = new ChatDatabaseTransport();
var service = new MessagingService(transport);

service.OnReceiveMessage += message =>
{
    Console.WriteLine($"{message.HandleId}: {message.Text}");
};

using var cts = new CancellationTokenSource();
await service.StartAsync(cts.Token);
```

`ChatDatabaseTransport()` defaults to `~/Library/Messages/chat.db`; pass a
path to override it (e.g. for testing against a copy).

## Sending

Plain text, default account:

```csharp
await service.SendMessageAsync(chatIdentifier, new OutboundMessage("hello"));
```

`chatIdentifier` is the `chat_identifier` column from `chat.db` (a phone
number/email for a 1:1 chat, or `chatXXXXXXXXXX` for a group). Internally
this resolves to a chat GUID (`iMessage;-;<id>` or `iMessage;+;<id>` for
groups) and requires that chat to already exist -- the injector's default
send command doesn't create new chats.

Sending a payload (e.g. GamePigeon -- see `../GamePigeonProtocol/README.md`
for building `RawPayload`/`BalloonBundleId`):

```csharp
await service.SendMessageAsync(chatIdentifier, new OutboundMessage(
    Text: "your turn",
    RawPayload: payloadBytes,
    BalloonBundleId: "com.apple.messages.MSMessageExtensionBalloonPlugin:EWFNLB79LQ:com.gamerdelights.gamepigeon.ext"));
```

Sending from a specific account/alias (plain text only -- `RawPayload`/
`BalloonBundleId` throw `NotSupportedException` when combined with
`SenderAccountUniqueId`):

```csharp
var accounts = await service.ListAccountsAsync();
var account = accounts.First(a => a.Aliases.Length > 0);

await service.SendMessageAsync(recipientHandleId, new OutboundMessage(
    Text: "hello",
    SenderAccountUniqueId: account.UniqueId,
    SenderIdentityId: "+15305550143")); // optional: force the "from" alias
```

This path (`sendViaAccount`) creates the chat if it doesn't exist yet, so
`chatIdentifier` here is the recipient's handle, not a pre-existing chat's
identifier.

## Requirements

- macOS, Full Disk Access granted to the process reading `chat.db`.
- `MessagesInjector`'s patched `Messages.app` running and its socket
  present at `/tmp/gamepigeonfucker-injector.sock` -- `SendAsync`/
  `ListAccountsAsync` throw if it isn't. See `../MessagesInjector/README.md`
  for how to build and run it. `WatchAsync` (read-only) works without it.

## Sample host: the `IMessageInterceptor` executable

The project also builds a console entry point (`ProgramOptions`,
`SendGate`, `ProcessedMessageTracker`, `MessageLogger`) used by
`../GamePigeonFucker`'s `Program.cs` as a reference implementation --
watches `chat.db`, logs every inbound message to a file, and gates replies
behind `--send-mode disabled|confirm|enabled` (`disabled` prints a
`[DRY-RUN]` line instead of sending; `confirm` prompts on the console per
message; `enabled` sends unconditionally). `ProcessedMessageTracker`
dedupes by message GUID so a reply handler only fires once per message.
These pieces are internal helpers for that host, not part of the library's
public surface -- use `MessagingService` directly for your own host.
