namespace GamePigeon.Games;

/// <summary>
/// Turns a solver's raw move decision into a correct next game state: player1/player2
/// identity, slot assignment, winner determination, and message sequencing all live here,
/// in the protocol, so solvers only ever decide *what* to play, never *how* to encode it.
/// </summary>
internal interface IGamePigeonMoveHandler<TState, in TMove> where TState : GamePigeonGameState
{
    TState ApplyMove(TState state, string playerUuid, string? playerAvatar, TMove move);
}
