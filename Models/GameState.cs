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
    public bool IsInvisible { get; set; }
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
        IsInvisible = false;
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
        CurrentCol = GetOptimalSpawnCol(matrix);
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
    /// Determines the optimal column to spawn a piece.
    /// Uses centre until any column has blocks within 5 rows of the top visible area,
    /// then picks the column region with the most free space.
    /// </summary>
    private int GetOptimalSpawnCol(int[,] matrix)
    {
        int pieceWidth = matrix.GetLength(1);
        int centreCol = (_config.Columns - pieceWidth) / 2;

        // Threshold: if no column has a block above this row, use centre.
        // "8 rows from the bottom" of the visible area = internal row (TotalRows - 8)
        int threshold = Board.TotalRows - 8;

        // Find the highest occupied row per column (lowest internal row number = highest)
        bool anyAboveThreshold = false;
        int[] columnHeights = new int[_config.Columns];
        for (int col = 0; col < _config.Columns; col++)
        {
            columnHeights[col] = Board.TotalRows; // default: empty (bottom)
            for (int row = _config.BufferRows; row < Board.TotalRows; row++)
            {
                if (Board.GetCellInternal(row, col) != null)
                {
                    columnHeights[col] = row;
                    if (row <= threshold)
                        anyAboveThreshold = true;
                    break;
                }
            }
        }

        if (!anyAboveThreshold)
            return centreCol;

        // Score each valid spawn column by the minimum height in the columns it would occupy.
        // Higher columnHeights[c] value = more space (piece is further from top).
        int bestCol = centreCol;
        int bestMinHeight = -1;

        int maxCol = _config.Columns - pieceWidth;
        for (int col = 0; col <= maxCol; col++)
        {
            // Find the minimum height (worst/most crowded) among the columns this piece spans
            int minHeight = Board.TotalRows;
            for (int pc = 0; pc < pieceWidth; pc++)
            {
                if (columnHeights[col + pc] < minHeight)
                    minHeight = columnHeights[col + pc];
            }

            // Prefer positions with more headroom (higher minHeight value)
            // On ties, prefer position closer to centre
            if (minHeight > bestMinHeight ||
                (minHeight == bestMinHeight && Math.Abs(col - centreCol) < Math.Abs(bestCol - centreCol)))
            {
                bestMinHeight = minHeight;
                bestCol = col;
            }
        }

        // Verify no collision at the chosen column; fall back to centre if blocked
        if (Board.HasCollision(matrix, _config.BufferRows, bestCol))
            return centreCol;

        return bestCol;
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

    /// <summary>
    /// Calculate the deepest valid position for an invisible piece.
    /// Scans from the bottom of the board upward, finding the lowest row
    /// where the piece fits without overlapping existing cells or going out of bounds.
    /// Returns -1 if no valid position exists.
    /// </summary>
    public int GetInvisibleGhostRow()
    {
        if (CurrentPiece == null) return CurrentRow;

        var matrix = CurrentPiece.CurrentMatrix;

        // Scan from bottom to top. Start at TotalRows-1 and let HasCollision
        // handle boundary checks (it only checks filled cells, so empty matrix
        // rows extending past the board are fine).
        for (int testRow = Board.TotalRows - 1; testRow >= 0; testRow--)
        {
            if (!Board.HasCollision(matrix, testRow, CurrentCol))
            {
                return testRow;
            }
        }

        // No valid position found anywhere
        return -1;
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
