namespace Tetris.Models;

using Tetris.Config;

/// <summary>
/// Represents the game grid. Handles collision detection and line clearing.
/// </summary>
public class GameBoard
{
    private readonly GameConfig _config;
    private readonly TetrominoShape?[,] _grid;

    public int Columns => _config.Columns;
    public int Rows => _config.Rows;
    public int TotalRows => _config.Rows + _config.BufferRows;

    public GameBoard(GameConfig config)
    {
        _config = config;
        _grid = new TetrominoShape?[TotalRows, Columns];
    }

    /// <summary>
    /// Get the cell value at a visible row/col. Row 0 is the top visible row.
    /// Internally offset by BufferRows.
    /// </summary>
    public TetrominoShape? GetCell(int row, int col)
    {
        int internalRow = row + _config.BufferRows;
        if (internalRow < 0 || internalRow >= TotalRows || col < 0 || col >= Columns)
            return null;
        return _grid[internalRow, col];
    }

    /// <summary>
    /// Get the cell value using internal coordinates (including buffer).
    /// </summary>
    public TetrominoShape? GetCellInternal(int internalRow, int col)
    {
        if (internalRow < 0 || internalRow >= TotalRows || col < 0 || col >= Columns)
            return null;
        return _grid[internalRow, col];
    }

    /// <summary>
    /// Check if a piece at the given position would collide with the board or boundaries.
    /// Position is in internal coordinates.
    /// </summary>
    public bool HasCollision(int[,] matrix, int pieceRow, int pieceCol)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (matrix[r, c] == 0) continue;

                int boardRow = pieceRow + r;
                int boardCol = pieceCol + c;

                if (boardCol < 0 || boardCol >= Columns || boardRow >= TotalRows)
                    return true;

                if (boardRow < 0) continue;

                if (_grid[boardRow, boardCol] != null)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Lock a piece into the grid at the given position.
    /// </summary>
    public void LockPiece(TetrominoShape shape, int[,] matrix, int pieceRow, int pieceCol)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (matrix[r, c] == 0) continue;

                int boardRow = pieceRow + r;
                int boardCol = pieceCol + c;

                if (boardRow >= 0 && boardRow < TotalRows && boardCol >= 0 && boardCol < Columns)
                {
                    _grid[boardRow, boardCol] = shape;
                }
            }
        }
    }

    /// <summary>
    /// Clear completed lines and return the number cleared.
    /// </summary>
    public int ClearLines()
    {
        int linesCleared = 0;

        for (int row = TotalRows - 1; row >= 0; row--)
        {
            if (IsRowFull(row))
            {
                RemoveRow(row);
                linesCleared++;
                row++; // Re-check this row since rows shifted down
            }
        }

        return linesCleared;
    }

    /// <summary>
    /// Check if any cells in the buffer zone are occupied (game over condition).
    /// </summary>
    public bool IsBufferOccupied()
    {
        for (int row = 0; row < _config.BufferRows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                if (_grid[row, col] != null)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Reset the board to empty.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_grid);
    }

    /// <summary>
    /// Remove up to 'count' occupied bottom rows as a level-up reward.
    /// Returns the number of rows actually removed.
    /// </summary>
    public int RemoveBottomRows(int count)
    {
        int removed = 0;
        for (int i = 0; i < count; i++)
        {
            int bottomRow = TotalRows - 1 - i;
            if (bottomRow < _config.BufferRows) break;

            bool hasContent = false;
            for (int col = 0; col < Columns; col++)
            {
                if (_grid[bottomRow, col] != null)
                {
                    hasContent = true;
                    break;
                }
            }

            if (hasContent)
            {
                RemoveRow(bottomRow);
                removed++;
            }
        }
        return removed;
    }

    /// <summary>
    /// Apply per-cell gravity: each cell falls as far as it can independently.
    /// Returns true if any cells moved.
    /// </summary>
    public bool ApplyGravity()
    {
        bool moved = false;

        // Process columns independently, bottom-up
        for (int col = 0; col < Columns; col++)
        {
            for (int row = TotalRows - 2; row >= 0; row--)
            {
                if (_grid[row, col] == null) continue;

                // Find lowest empty cell below this one
                int targetRow = row;
                for (int below = row + 1; below < TotalRows; below++)
                {
                    if (_grid[below, col] == null)
                        targetRow = below;
                    else
                        break;
                }

                if (targetRow != row)
                {
                    _grid[targetRow, col] = _grid[row, col];
                    _grid[row, col] = null;
                    moved = true;
                }
            }
        }

        return moved;
    }

    private bool IsRowFull(int row)
    {
        for (int col = 0; col < Columns; col++)
        {
            if (_grid[row, col] == null)
                return false;
        }
        return true;
    }

    private void RemoveRow(int targetRow)
    {
        for (int row = targetRow; row > 0; row--)
        {
            for (int col = 0; col < Columns; col++)
            {
                _grid[row, col] = _grid[row - 1, col];
            }
        }
        // Clear top row
        for (int col = 0; col < Columns; col++)
        {
            _grid[0, col] = null;
        }
    }
}
