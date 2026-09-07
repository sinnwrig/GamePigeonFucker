IMessageInterceptor: .NET class library, watch/send iMessage on macOS via
MessagesInjector's Unix socket (`../MessagesInjector/`). No chat.db, no
Full Disk Access. Events pushed live from an `NSNotificationCenter`
observer inside the patched Messages.app process. Sends go through
IMCore's private send path (supports `balloonBundleId`/`payloadData`,
per-account send).

net10.0. No third-party deps.

## Types

```csharp
interface IMessageTransport
{
    Task WatchAsync(Action<InboundMessage> onMessage, CancellationToken ct);
    Task SendAsync(string chatIdentifier, OutboundMessage message);
    Task<IReadOnlyList<ImAccountInfo>> ListAccountsAsync();
}
```
Implementation: `LiveMessageTransport`. Wraps `InjectorClient`.

```csharp
abstract record ChatEvent(string? ChatIdentifier, string? ChatGuid, string MessageGuid, DateTime Timestamp, bool IsFromMe);

record NewMessageEvent(..., string? Text, string? SenderHandleId, string? BalloonBundleId, byte[]? PayloadData, bool IsReply, string? ThreadIdentifier);
record ReactionEvent(..., string? TargetMessageGuid, int AssociatedMessageType, string? Emoji);
record ReadReceiptEvent(..., DateTime ReadAt);
record DeliveryStatusEvent(..., bool IsDelivered, DateTime? DeliveredAt);
record MessageEditedEvent(..., DateTime EditedAt);
record MessageRetractedEvent(...);
record ChatResyncEvent(...);
```
Produced by `ChatEventClassifier` off raw `new`/`old` message snapshots.
Native side does no interpretation.

Classifier state: per-guid, tracks whether text and/or payload have been
seen yet (`_seenGuids`, `_guidsWithText`, `_guidsWithPayload`). A
`NewMessageEvent`/`ReactionEvent` fires when text or payload first appears
for a guid - not just on first sighting of the guid. First sighting with
no text, no payload, not a reaction -> classifier returns `null`, nothing
is emitted. This means a guid's content can arrive across multiple
notifications and still produce exactly one `NewMessageEvent` at the point
it becomes non-empty.

`AssociatedMessageType` numeric meaning (add/remove, which tapback) not
verified against this injector yet.

```csharp
sealed class MessagingService(IMessageTransport transport)
{
    event Action<InboundMessage>? OnReceiveMessage; // fires for NewMessageEvent only
    Task StartAsync(CancellationToken ct);
    Task SendMessageAsync(string chatIdentifier, OutboundMessage message);
    Task<IReadOnlyList<ImAccountInfo>> ListAccountsAsync();
}
```
For full event stream (reactions, receipts, edits, ...), call
`LiveMessageTransport.WatchEventsAsync` directly instead.

```csharp
record InboundMessage(string Guid, string ChatIdentifier, string HandleId, string Text, string BalloonBundleId, byte[]? PayloadData, DateTimeOffset Timestamp, bool IsFromMe);
record OutboundMessage(string Text, byte[]? RawPayload = null, string? BalloonBundleId = null, string? SenderAccountUniqueId = null, string? SenderIdentityId = null);
record ImAccountInfo(string ClassName, string UniqueId, string LoginImHandle, string Aliases, string LoginHandles)
{
    IReadOnlyList<string> AliasList { get; }       // parsed from Aliases
    IReadOnlyList<string> LoginHandleList { get; } // parsed from LoginHandles
}
```
`InboundMessage` is a projection of `NewMessageEvent`. Non-empty
`AliasList` = real send-capable account. See `MessagesInjector/README.md`.

## Usage

```csharp
var transport = new LiveMessageTransport();
var service = new MessagingService(transport);
service.OnReceiveMessage += m => Console.WriteLine($"{m.HandleId}: {m.Text}");
using var cts = new CancellationTokenSource();
await service.StartAsync(cts.Token);
```

Full event stream:

```csharp
var transport = new LiveMessageTransport();
await transport.WatchEventsAsync(e =>
{
    switch (e)
    {
        case NewMessageEvent m: Console.WriteLine($"{m.SenderHandleId}: {m.Text}"); break;
        case ReactionEvent r: Console.WriteLine($"reaction {r.AssociatedMessageType} on {r.TargetMessageGuid}"); break;
        case ReadReceiptEvent rr: Console.WriteLine($"read at {rr.ReadAt}"); break;
    }
}, cts.Token);
```

`LiveMessageTransport()` default socket: `/tmp/gamepigeonfucker-injector.sock`.
Constructor arg overrides.

### Remote injector (dev on a different machine)

```
ssh -N -L /tmp/gpf-injector.sock:/tmp/gamepigeonfucker-injector.sock user@mac-host
```
```csharp
var transport = new LiveMessageTransport("/tmp/gpf-injector.sock");
```

Or managed:
```csharp
using var tunnel = await SshInjectorTunnel.StartAsync("user@mac-host");
var transport = new LiveMessageTransport(tunnel.LocalSocketPath);
```
Local: skip both, `new LiveMessageTransport()`.

## Sending

Plain text, default account:
```csharp
await service.SendMessageAsync(chatIdentifier, new OutboundMessage("hello"));
```
`chatIdentifier`: phone/email (1:1) or `chatXXXXXXXXXX` (group), same as
`ChatEvent.ChatIdentifier`. Resolves to `iMessage;-;<id>` /
`iMessage;+;<id>`. Chat must already exist -- returns `ok: false` /
"chat not found" otherwise.

Payload send (see `../GamePigeonProtocol/README.md`):
```csharp
await service.SendMessageAsync(chatIdentifier, new OutboundMessage(
    Text: "your turn",
    RawPayload: payloadBytes,
    BalloonBundleId: "com.apple.messages.MSMessageExtensionBalloonPlugin:EWFNLB79LQ:com.gamerdelights.gamepigeon.ext"));
```

Send from specific account/alias (plain text only; `RawPayload`/
`BalloonBundleId` + `SenderAccountUniqueId` throws `NotSupportedException`):
```csharp
var accounts = await service.ListAccountsAsync();
var account = accounts.First(a => a.AliasList.Count > 0);
await service.SendMessageAsync(recipientHandleId, new OutboundMessage(
    Text: "hello",
    SenderAccountUniqueId: account.UniqueId,
    SenderIdentityId: account.AliasList[0])); // optional: force "from" alias
```
`sendViaAccount` creates the chat if missing -- `chatIdentifier` here is
the recipient's handle, not an existing chat's identifier.

## Requirements

- macOS.
- Patched `Messages.app` (`MessagesInjector`) running, socket at
  `/tmp/gamepigeonfucker-injector.sock`. No disk fallback. See
  `../MessagesInjector/README.md` (build/run, SIP caveat).

## Console host (`IMessageInterceptor` executable)

`ProgramOptions`, `SendGate`, `ProcessedMessageTracker`, `MessageLogger` --
reference host used by `../GamePigeonFucker/Program.cs`. Watches inbound
messages, logs to file, gates replies via `--send-mode disabled|confirm|enabled`:
- `disabled`: prints `[DRY-RUN]`, does not send.
- `confirm`: prompts per message.
- `enabled`: sends unconditionally.

`ProcessedMessageTracker` dedupes by message GUID, one fire per message.
Internal to this host, not part of the library surface -- use
`MessagingService`/`LiveMessageTransport` directly for other hosts.
