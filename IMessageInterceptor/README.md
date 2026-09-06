`IMessageInterceptor` is a .NET class library for watching and sending
iMessages on macOS. It has no direct dependency on `chat.db` or Full Disk
Access - all reads and sends go through `MessagesInjector`'s Unix socket
(`../MessagesInjector/`), a patched copy of `Messages.app` running inside
the real Messages process. Incoming events (new messages, reactions, read
receipts, edits, ...) are pushed live from an `NSNotificationCenter`
observer registered inside that process, not polled from disk. Sending
uses IMCore's private send path directly, which is what lets it set a
GamePigeon `balloonBundleId`/`payloadData` and choose which registered
account/alias a message is sent from - neither of which AppleScript's
`send` command can do.

Target framework: net10.0. No third-party package dependencies.

## Core types

- `IMessageTransport` -- the abstraction: `WatchAsync`, `SendAsync`,
  `ListAccountsAsync`. `LiveMessageTransport` is the only implementation:
  it subscribes to `InjectorClient`'s push stream for inbound events and
  calls it for outbound sends and account listing.
- `ChatEvent` -- an abstract record with one case per kind of event the
  injector can push, so consumers pattern-match on `is` rather than
  inspect a flat "message" shape for a `type` field:
  - `NewMessageEvent` -- a fresh message insertion (`Text`,
    `SenderHandleId`, `BalloonBundleId`, `PayloadData`, `IsReply`,
    `ThreadIdentifier`).
  - `ReactionEvent` -- a tapback/reaction (`TargetMessageGuid`,
    `AssociatedMessageType`, `Emoji`). `AssociatedMessageType`'s exact
    numeric meaning (add vs. remove, which tapback) has not been
    empirically verified against this injector yet - treat it as raw
    until confirmed live.
  - `ReadReceiptEvent` -- `ReadAt` changed on an existing message.
  - `DeliveryStatusEvent` -- `IsDelivered`/`DeliveredAt` changed.
  - `MessageEditedEvent` -- `EditedAt` changed (`hasEditedParts`).
  - `MessageRetractedEvent` -- a part of the message was unsent.
  - `ChatResyncEvent` -- catch-all for a state notification that didn't
    match any of the above (e.g. the burst of re-notifications Messages
    fires for existing messages right after the app (re)launches).

  All cases share `ChatIdentifier`, `ChatGuid`, `MessageGuid`,
  `Timestamp`, `IsFromMe`. Classification is pure C# (`ChatEventClassifier`)
  working off the raw `new`/`old` message snapshots the injector forwards -
  the native side does no interpretation, it only pipes fields through.
- `MessagingService` -- thin wrapper around a transport. Exposes
  `OnReceiveMessage` (fires only for `NewMessageEvent`, kept for simple
  consumers), `StartAsync(cancellationToken)`,
  `SendMessageAsync(chatIdentifier, OutboundMessage)`,
  `ListAccountsAsync()`. For the full event stream, call
  `LiveMessageTransport.WatchEventsAsync` directly instead of going
  through `MessagingService`.
- `InboundMessage` (record) -- `Guid`, `ChatIdentifier`, `HandleId`, `Text`,
  `BalloonBundleId`, `PayloadData`, `Timestamp`, `IsFromMe`. This is a
  projection of `NewMessageEvent` kept for existing simple consumers;
  new code should prefer `ChatEvent` for full fidelity.
- `OutboundMessage` (record) -- `Text`, `RawPayload`, `BalloonBundleId`,
  `SenderAccountUniqueId`, `SenderIdentityId`. Leave the last three null for
  a plain-text send through the default account.
- `ImAccountInfo` (record) -- `ClassName`, `UniqueId`, `LoginImHandle`,
  `Aliases`, `LoginHandles`, as returned by `listAccounts` on the injector.
  `AliasList`/`LoginHandleList` parse those raw NSArray-description
  strings into `IReadOnlyList<string>`. Only an account with a non-empty
  `AliasList` is a real send-capable iMessage identity (see
  `MessagesInjector/README.md`).

## Basic usage

```csharp
using IMessage;

var transport = new LiveMessageTransport();
var service = new MessagingService(transport);

service.OnReceiveMessage += message =>
{
    Console.WriteLine($"{message.HandleId}: {message.Text}");
};

using var cts = new CancellationTokenSource();
await service.StartAsync(cts.Token);
```

For the full typed event stream instead of just new messages:

```csharp
var transport = new LiveMessageTransport();
using var cts = new CancellationTokenSource();

await transport.WatchEventsAsync(chatEvent =>
{
    switch (chatEvent)
    {
        case NewMessageEvent m:
            Console.WriteLine($"{m.SenderHandleId}: {m.Text}");
            break;
        case ReactionEvent r:
            Console.WriteLine($"reaction {r.AssociatedMessageType} on {r.TargetMessageGuid}");
            break;
        case ReadReceiptEvent rr:
            Console.WriteLine($"read at {rr.ReadAt}");
            break;
    }
}, cts.Token);
```

`LiveMessageTransport()` defaults to `/tmp/gamepigeonfucker-injector.sock`;
pass a path to override it.

### Connecting to a remote injector (LAN iteration)

`LiveMessageTransport`'s constructor argument overrides the injector's
Unix socket path - useful when developing on a machine other than the one
running `Messages-patched.app` (e.g. a Mac that must keep SIP disabled
for the injector to launch, while you build/run the .NET side elsewhere).
Forward the remote socket to a local one over SSH (OpenSSH supports
Unix-socket-to-Unix-socket forwarding):

```
ssh -N -L /tmp/gpf-injector.sock:/tmp/gamepigeonfucker-injector.sock user@mac-host
```

Then point the transport at the local forwarded path:

```csharp
var transport = new LiveMessageTransport("/tmp/gpf-injector.sock");
```

There is no database to sync for this mode - everything, including
history-free live watching, goes through the socket.

## Sending

Plain text, default account:

```csharp
await service.SendMessageAsync(chatIdentifier, new OutboundMessage("hello"));
```

`chatIdentifier` is a phone number/email for a 1:1 chat, or
`chatXXXXXXXXXX` for a group - the same value you'd see as
`ChatIdentifier` on a received `ChatEvent`. Internally this resolves to a
chat GUID (`iMessage;-;<id>` or `iMessage;+;<id>` for groups) and requires
that chat to already exist on the Mac - the injector's default send
command doesn't create new chats, and returns `ok: false` with a "chat
not found" error if it doesn't.

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
var account = accounts.First(a => a.AliasList.Count > 0);

await service.SendMessageAsync(recipientHandleId, new OutboundMessage(
    Text: "hello",
    SenderAccountUniqueId: account.UniqueId,
    SenderIdentityId: account.AliasList[0])); // optional: force the "from" alias
```

This path (`sendViaAccount`) creates the chat if it doesn't exist yet, so
`chatIdentifier` here is the recipient's handle, not a pre-existing chat's
identifier.

## Requirements

- macOS.
- `MessagesInjector`'s patched `Messages.app` running and its socket
  present at `/tmp/gamepigeonfucker-injector.sock` -- every method on
  `LiveMessageTransport`, including `WatchAsync`/`WatchEventsAsync`,
  requires it now (there is no disk-based fallback). See
  `../MessagesInjector/README.md` for how to build and run it, including
  the SIP caveat for actually launching the patched app.

## Sample host: the `IMessageInterceptor` executable

The project also builds a console entry point (`ProgramOptions`,
`SendGate`, `ProcessedMessageTracker`, `MessageLogger`) used by
`../GamePigeonFucker`'s `Program.cs` as a reference implementation --
watches for inbound messages, logs each to a file, and gates replies
behind `--send-mode disabled|confirm|enabled` (`disabled` prints a
`[DRY-RUN]` line instead of sending; `confirm` prompts on the console per
message; `enabled` sends unconditionally). `ProcessedMessageTracker`
dedupes by message GUID so a reply handler only fires once per message.
These pieces are internal helpers for that host, not part of the library's
public surface -- use `MessagingService`/`LiveMessageTransport` directly
for your own host.
