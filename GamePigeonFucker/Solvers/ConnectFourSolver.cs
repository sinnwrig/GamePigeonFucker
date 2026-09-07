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

        if (!state.CanRespond(message.IsFromMe))
        {
            Console.WriteLine("[connect4] skipping: not eligible to respond to this message (not our turn)");
            return;
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

            var move = FindOpenColumn(board, Columns);
            if (move is not { } chosen)
            {
                Console.WriteLine("[connect4] skipping: board is full");
                return;
            }

            var nextPlayer = state.LastMove is { Player: var lastPlayer } ? (lastPlayer == 1 ? 2 : 1) : 1;
            board[chosen.Row * Columns + chosen.Column] = nextPlayer;

            var nextState = state with
            {
                Size = Columns,
                Board = board,
                LastMove = (chosen.Column, chosen.Row, nextPlayer),
                MessageNumber = (state.MessageNumber ?? 0) + 1,
            };

            Console.WriteLine($"[connect4] sending move column={chosen.Column} row={chosen.Row} player={nextPlayer} to {message.ChatIdentifier}");
            var sent = await gamer.Dispatcher.SendMoveAsync(nextState, service, message.ChatIdentifier, gamer.PlayerUuid, gamer.PlayerAvatar, "Connect Four move");
            Console.WriteLine($"[connect4] move sent={sent}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[connect4] failed: {ex}");
        }
    }

    private static (int Column, int Row)? FindOpenColumn(IReadOnlyList<int> board, int size)
    {
        var rows = board.Count / size;
        for (var column = 0; column < size; column++)
        {
            for (var row = rows - 1; row >= 0; row--)
            {
                if (board[row * size + column] == 0)
                {
                    return (column, row);
                }
            }
        }

        return null;
    }
}
