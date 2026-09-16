using IMessage;
using GamePigeon.Games;


public sealed class ConnectFourSolver : IGameSolver
{
    public string Name => "connect4";

    private const int Columns = 7;
    private const int Rows = 6;

    public void Register(GamePigeonDispatcher dispatcher, Gamer gamer)
    {
        dispatcher.OnGame<ConnectFourState>((state, message) => Solve(state, message, gamer));
    }

    private async void Solve(ConnectFourState state, InboundMessage message, Gamer gamer)
    {
        if (!gamer.IsEnabled(Name))
            return;

        if (!gamer.Dispatcher.CanRespond(state, message.IsFromMe))
        {
            Console.WriteLine("[connect4] skipping: not eligible to respond to this message (not our turn)");
            return;
        }

        if (message.IsFromMe && !await gamer.WaitForDeliveryAsync(message.Guid, TimeSpan.FromSeconds(30)))
        {
            // Balloon updates to a still-in-flight own message fall back to RCS/SMS and
            // lose the balloon; give up waiting after 30s rather than never respond.
            Console.WriteLine("[connect4] own invite delivery not confirmed after 30s; sending anyway");
        }

        try
        {
            Console.WriteLine($"Got a connect 4 game: size={state.Size} boardLen={state.Board?.Count} board=[{(state.Board is null ? "" : string.Join(',', state.Board))}] lastMove={state.LastMove} winner={state.WinnerId} rawFields=[{string.Join(',', state.RawFields.Select(kv => $"{kv.Key}={kv.Value}"))}]");

            if (gamer.Service is not { } service)
            {
                Console.WriteLine("[connect4] skipping: no service");
                return;
            }

            var board = state.Board?.ToArray() ?? new int[Rows * Columns];

            var column = FindOpenColumn(board, Columns);
            if (column is not { } chosenColumn)
            {
                Console.WriteLine("[connect4] skipping: board is full");
                return;
            }

            Console.WriteLine($"[connect4] sending move column={chosenColumn} to {message.ChatIdentifier}");
            var move = new ConnectFourMove(chosenColumn);
            var sent = await gamer.Dispatcher.SendMoveAsync(state, move, service, message.ChatIdentifier, gamer.PlayerUuid, gamer.PlayerAvatar, "Connect Four move");
            Console.WriteLine($"[connect4] move sent={sent}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[connect4] failed: {ex}");
        }
    }

    private static int? FindOpenColumn(IReadOnlyList<int> board, int size)
    {
        for (var column = 0; column < size; column++)
        {
            if (board[column] == 0)
            {
                return column;
            }
        }

        return null;
    }
}
