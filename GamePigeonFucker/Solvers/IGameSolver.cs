using GamePigeon.Games;


public interface IGameSolver
{
    string Name { get; }

    void Register(GamePigeonDispatcher dispatcher, Gamer gamer);
}
