namespace Tetris.Models;

using Tetris.Config;

/// <summary>
/// Holds the complete state of a Tetris game.
/// </summary>
public class GameState
{
    private readonly GameConfig _config;
    private readonly Random _random = new();
    private readonly List<TetrominoShape> _bag = [];

    public GameBoard Board { get; }
    public Tetromino? CurrentPiece { get; set; }
    public int CurrentRow { get; set; }
    public int CurrentCol { get; set; }
    public Queue<TetrominoShape> NextQueue { get; } = new();
    public Tetromino? HoldPiece { get; set; }

    public int Score { get; set; }
    public int Level { get; set; }
    public int LinesCleared { get; set; }
    public int TotalLinesCleared { get; set; }
    public int TetrisCount { get; set; }
    public int CascadeCount { get; set; }
    public int CascadeLines { get; set; }
    public bool IsGameOver { get; set; }
    public bool IsPaused { get; set; }

    public GameState(GameConfig config)
    {
        _config = config;
        Board = new GameBoard(config);
        Level = config.StartingLevel;
    }

    /// <summary>
    /// Initialize the game: fill the next queue and spawn the first piece.
    /// </summary>
    public void Initialize()
    {
        Score = 0;
        Level = _config.StartingLevel;
        LinesCleared = 0;
        TotalLinesCleared = 0;
        TetrisCount = 0;
        CascadeCount = 0;
        CascadeLines = 0;
        IsGameOver = false;
        IsPaused = false;
        HoldPiece = null;
        Board.Clear();
        NextQueue.Clear();
        _bag.Clear();

        // Fill the next queue
        while (NextQueue.Count < _config.PreviewCount + 1)
        {
            NextQueue.Enqueue(GetNextFromBag());
        }

        SpawnNextPiece();
    }

    /// <summary>
    /// Spawn the next piece from the queue.
    /// </summary>
    public bool SpawnNextPiece()
    {
        var shape = NextQueue.Dequeue();
        NextQueue.Enqueue(GetNextFromBag());

        CurrentPiece = new Tetromino(shape);

        // Spawn at top of visible area (internal row = BufferRows = visible row 0)
        var matrix = CurrentPiece.CurrentMatrix;
        CurrentCol = (_config.Columns - matrix.GetLength(1)) / 2;
        CurrentRow = _config.BufferRows;

        // Check if spawn position has collision (game over)
        if (Board.HasCollision(matrix, CurrentRow, CurrentCol))
        {
            IsGameOver = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Add lines cleared this turn, update level.
    /// </summary>
    public void AddLinesCleared(int lines)
    {
        if (lines <= 0) return;

        TotalLinesCleared += lines;
        LinesCleared += lines;
        Score += _config.GetLineClearScore(lines, Level);

        if (lines >= 4)
            TetrisCount++;

        // Level up check - each level requires different lines
        int linesNeeded = _config.GetLinesForLevel(Level);
        while (LinesCleared >= linesNeeded)
        {
            LinesCleared -= linesNeeded;
            Level++;
            linesNeeded = _config.GetLinesForLevel(Level);
        }
    }

    /// <summary>
    /// Lines remaining to complete the current level.
    /// </summary>
    public int LinesRemaining => _config.GetLinesForLevel(Level) - LinesCleared;

    /// <summary>
    /// Calculate where the current piece would land (ghost piece position).
    /// </summary>
    public int GetGhostRow()
    {
        if (CurrentPiece == null) return CurrentRow;

        int ghostRow = CurrentRow;
        while (!Board.HasCollision(CurrentPiece.CurrentMatrix, ghostRow + 1, CurrentCol))
        {
            ghostRow++;
        }
        return ghostRow;
    }

    private TetrominoShape GetNextFromBag()
    {
        if (_bag.Count == 0)
        {
            // 7-bag randomizer: shuffle all 7 shapes
            _bag.AddRange(Enum.GetValues<TetrominoShape>());
            // Fisher-Yates shuffle
            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
        }

        var shape = _bag[0];
        _bag.RemoveAt(0);
        return shape;
    }
}
