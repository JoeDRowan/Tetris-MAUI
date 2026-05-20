namespace Tetris.Services;

using Tetris.Config;

/// <summary>
/// Calculates scores for game actions.
/// </summary>
public class ScoreService
{
    private readonly GameConfig _config;

    public ScoreService(GameConfig config)
    {
        _config = config;
    }

    public int GetLineClearScore(int linesCleared, int level) => _config.GetLineClearScore(linesCleared, level);
}
