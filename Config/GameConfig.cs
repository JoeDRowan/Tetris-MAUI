namespace Tetris.Config;

using Microsoft.Maui.Graphics;

/// <summary>
/// Central configuration for all game parameters.
/// Adjust these values to tune gameplay without changing logic.
/// </summary>
public class GameConfig
{
    // Board dimensions
    public int Columns { get; set; } = 10;
    public int Rows { get; set; } = 20;
    public int BufferRows { get; set; } = 4;

    // Piece queue
    public int PreviewCount { get; set; } = 2;

    // Levels & progression
    public int StartingLevel { get; set; } = 1;
    public int LinesPerLevel { get; set; } = 10;

    // Timing (milliseconds)
    public double BaseDropIntervalMs { get; set; } = 1000;
    public double SpeedMultiplierPerLevel { get; set; } = 0.85;
    public double MinDropIntervalMs { get; set; } = 100;
    public double SoftDropSpeedMultiplier { get; set; } = 20;
    public double LockDelayMs { get; set; } = 500;

    // Scoring
    public int PointsPerSoftDrop { get; set; } = 1;
    public int PointsPerHardDrop { get; set; } = 2;
    public int[] LineClearPoints { get; set; } = [100, 300, 500, 800];

    // Colors per tetromino shape (I, O, T, S, Z, J, L)
    public Dictionary<TetrominoShape, Color> PieceColors { get; set; } = new()
    {
        { TetrominoShape.I, Colors.Cyan },
        { TetrominoShape.O, Colors.Yellow },
        { TetrominoShape.T, Colors.Purple },
        { TetrominoShape.S, Colors.Green },
        { TetrominoShape.Z, Colors.Red },
        { TetrominoShape.J, Colors.Blue },
        { TetrominoShape.L, Colors.Orange },
    };

    // Visual
    public float CellSize { get; set; } = 30f;
    public float GhostPieceOpacity { get; set; } = 0.3f;
    public float BoardBorderWidth { get; set; } = 2f;
    public Color BoardBorderColor { get; set; } = Colors.White;
    public Color BoardBackgroundColor { get; set; } = Color.FromRgb(20, 20, 30);
    public Color GridLineColor { get; set; } = Color.FromRgba(60, 60, 80, 100);

    /// <summary>
    /// Calculate drop interval for a given level.
    /// </summary>
    public double GetDropIntervalMs(int level)
    {
        var interval = BaseDropIntervalMs * Math.Pow(SpeedMultiplierPerLevel, level - 1);
        return Math.Max(interval, MinDropIntervalMs);
    }

    /// <summary>
    /// Calculate score for clearing lines at a given level.
    /// </summary>
    public int GetLineClearScore(int linesCleared, int level)
    {
        if (linesCleared < 1 || linesCleared > LineClearPoints.Length)
            return 0;
        return LineClearPoints[linesCleared - 1] * level;
    }
}

public enum TetrominoShape
{
    I, O, T, S, Z, J, L
}
