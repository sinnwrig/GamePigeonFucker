using IMessage;
using GamePigeon.Games;


public sealed class WordHuntSolver : IGameSolver
{
    public string Name => "hunt";

    private const int GridSize = 4;
    private static readonly TimeSpan ResponseDelay = TimeSpan.FromSeconds(5);

    private static readonly Lazy<WordDefinitions> Definitions = new(() =>
        WordDefinitions.LoadFromEmbeddedResource("CollinsScrabbleWords2019.txt", GridSize * GridSize));

    public void Register(GamePigeonDispatcher dispatcher, Gamer gamer)
    {
        dispatcher.OnGame<WordHuntState>((state, message) => Solve(state, message, gamer));
    }

    private async void Solve(WordHuntState state, InboundMessage message, Gamer gamer)
    {
        if (!gamer.IsEnabled(Name))
            return;

        if (!gamer.Dispatcher.CanRespond(state, message.IsFromMe))
        {
            Console.WriteLine("[hunt] skipping: not eligible to respond to this message (already-answered outgoing move)");
            return;
        }

        if (message.IsFromMe && !await gamer.WaitForDeliveryAsync(message.Guid, TimeSpan.FromSeconds(30)))
        {
            // Answering our own invite sends a balloon update to it; if the invite's own
            // send is still in-flight, the update falls back to RCS/SMS and the balloon
            // is lost. Give up waiting after 30s rather than never respond.
            Console.WriteLine("[hunt] own invite delivery not confirmed after 30s; sending anyway");
        }

        try
        {
            Console.WriteLine($"[hunt] message: guid={message.Guid} chat={message.ChatIdentifier} handle={message.HandleId} isFromMe={message.IsFromMe} balloon={message.BalloonBundleId} payloadLen={message.PayloadData?.Length} text=\"{message.Text}\"");
            Console.WriteLine($"[hunt] state: sessionSender={state.SessionSender} sessionId={state.SessionId} gameName={state.GameName} player1={state.Player1Id} player2={state.Player2Id} num={state.MessageNumber} letters={state.Letters} lang={state.Language} mode={state.Mode} score1={state.Score1} score2={state.Score2} words1={state.Words1} words2={state.Words2} wordsList1Count={state.WordsList1.Count} wordsList2Count={state.WordsList2.Count}");
            Console.WriteLine($"[hunt] rawFields=[{string.Join(',', state.RawFields.Select(kv => $"{kv.Key}={kv.Value}"))}]");
            Console.WriteLine($"[hunt] isOpenInvite={state.IsOpenInvite} turnMode={state.TurnMode} playerSlot={state.GetPlayerSlot(gamer.PlayerUuid)}");

            if (gamer.Service is not { } service)
            {
                Console.WriteLine("[hunt] skipping: no service");
                return;
            }

            if (state.Letters is not { Length: GridSize * GridSize } letters)
            {
                Console.WriteLine($"[hunt] skipping: expected {GridSize * GridSize} letters, got '{state.Letters}'");
                return;
            }

            char[,] grid = new char[GridSize, GridSize];
            for (int y = 0; y < GridSize; y++)
                for (int x = 0; x < GridSize; x++)
                    grid[x, y] = letters[y * GridSize + x];

            WordHuntGrid solver = new(Definitions.Value, grid);
            List<string> words = solver.FindAllWords().OrderByDescending(w => w.Length).ThenBy(w => w).ToList();
            int score = words.Sum(ScoreWord);

            // await service.SendMessageAsync(message.ChatIdentifier, new OutboundMessage("Hacking your game pigeon..."));
            await Task.Delay(ResponseDelay);

            Console.WriteLine($"[hunt] sending {words.Count} words (score={score}) to {message.ChatIdentifier}");
            var move = new WordHuntMove(words, score);
            var sent = await gamer.Dispatcher.SendMoveAsync(state, move, service, message.ChatIdentifier, gamer.PlayerUuid, gamer.PlayerAvatar, $"Found {words.Count} words");
            Console.WriteLine($"[hunt] move sent={sent}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[hunt] failed: {ex}");
        }
    }


    private static int ScoreWord(string word) => word.Length switch
    {
        <= 2 => 0,
        3 => 100,
        4 => 400,
        5 => 800,
        6 => 1400,
        7 => 1800,
        _ => 2200,
    };
}
