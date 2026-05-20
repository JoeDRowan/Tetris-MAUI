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
    public int PointsPerSoftDrop { get; set; } = 0;
    public int BasePointsPerLine { get; set; } = 10;
    public int TetrisBasePointsPerLine { get; set; } = 20;
    public double TetrisOverlayDurationMs { get; set; } = 1500;

    // Colors per tetromino shape (I, O, T, S, Z, J, L)
    public Dictionary<TetrominoShape, Color> PieceColors { get; set; } = new()
    {
        { TetrominoShape.I, Color.FromRgb(0, 180, 220) },
        { TetrominoShape.O, Color.FromRgb(220, 180, 0) },
        { TetrominoShape.T, Color.FromRgb(160, 50, 200) },
        { TetrominoShape.S, Color.FromRgb(50, 180, 50) },
        { TetrominoShape.Z, Color.FromRgb(220, 50, 50) },
        { TetrominoShape.J, Color.FromRgb(50, 80, 200) },
        { TetrominoShape.L, Color.FromRgb(230, 130, 20) },
    };

    // Visual
    public float CellSize { get; set; } = 30f;
    public float GhostPieceOpacity { get; set; } = 0.25f;
    public float BoardBorderWidth { get; set; } = 2f;
    public Color BoardBorderColor { get; set; } = Color.FromRgb(80, 80, 100);
    public Color BoardBackgroundColor { get; set; } = Color.FromRgb(240, 240, 248);
    public Color GridLineColor { get; set; } = Color.FromRgba(180, 180, 200, 120);

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
    /// 1-3 lines: 10 × lines × level
    /// 4 lines (Tetris): 20 × 4 × level
    /// </summary>
    public int GetLineClearScore(int linesCleared, int level)
    {
        if (linesCleared < 1) return 0;
        int basePoints = linesCleared >= 4 ? TetrisBasePointsPerLine : BasePointsPerLine;
        return basePoints * linesCleared * level;
    }
}

public enum TetrominoShape
{
    I, O, T, S, Z, J, L
}
