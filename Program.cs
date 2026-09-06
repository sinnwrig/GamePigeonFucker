using IMessage;
using GamePigeon;
using GamePigeon.Games;

var options = ProgramOptions.Parse(args);
using var logger = new MessageLogger(options.LogPath);
var transport = new ChatDatabaseTransport();
var service = new MessagingService(transport);
var sendGate = new SendGate(options.SendMode);
var processedTracker = new ProcessedMessageTracker();
var dispatcher = new GamePigeonDispatcher();

dispatcher.OnGame<ConnectFourState>((state, _) => Console.WriteLine($"[GAME:connect] board-size={state.Size} lastMove={state.LastMove} winner={state.WinnerId}"));
dispatcher.OnGame<AnagramsState>((state, _) => Console.WriteLine($"[GAME:anagrams] letters={state.Letters} score1={state.Score1} words1={state.Words1}"));
dispatcher.OnGame<WordHuntState>((state, _) => Console.WriteLine($"[GAME:hunt] letters={state.Letters} score2={state.Score2} words2={state.Words2}"));
dispatcher.OnGame<CupPongState>((state, _) => Console.WriteLine($"[GAME:beer] round={state.Round} score1={state.Score1} score2={state.Score2}"));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Watching chat.db for incoming iMessages. Logging to {options.LogPath}. Send mode: {options.SendMode}. Press Ctrl+C to stop.");

service.OnReceiveMessage += OnMessage;

try
{
    await service.StartAsync(cts.Token);
}
catch (OperationCanceledException)
{
}

return 0;

void OnMessage(InboundMessage message)
{
    Console.WriteLine($"[INTERCEPT] {MessageLogger.FormatLine(message)}");
    logger.Log(message);

    var decoded = message.TryDecodeGamePigeon(out var envelope);
    if (decoded)
    {
        Console.WriteLine($"[GAMEPIGEON] game={envelope.GameName} caption={envelope.UserInfo.GetValueOrDefault("caption")} session={envelope.SessionId} fields={string.Join(',', envelope.Fields.Select(kv => $"{kv.Key}={kv.Value}"))}");
        dispatcher.Dispatch(message);
    }

    if (message.IsGamePigeon() && !message.IsFromMe && processedTracker.TryMarkProcessed(message.Guid))
    {
        _ = HandleGamePigeonMessageAsync(message, decoded ? envelope.GameName : null);
    }
}

async Task HandleGamePigeonMessageAsync(InboundMessage message, string? gameName)
{
    var replyText = $"Received Game : {gameName ?? "Unknown"}";

    var shouldSend = await sendGate.ShouldSendAsync(message.ChatIdentifier, replyText);
    if (!shouldSend)
    {
        return;
    }

    try
    {
        await service.SendMessageAsync(message.ChatIdentifier, new OutboundMessage(replyText));
        Console.WriteLine($"[SEND-OK] chat={message.ChatIdentifier} text=\"{replyText}\"");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SEND-FAIL] chat={message.ChatIdentifier} error={ex.Message}");
    }
}
