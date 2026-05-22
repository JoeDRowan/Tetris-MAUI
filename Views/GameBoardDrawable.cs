namespace Tetris.Views;

using System.Collections.Generic;
using Microsoft.Maui.Graphics;
using Tetris.Config;
using Tetris.Models;

/// <summary>
/// Renders the main game board, active piece, and ghost piece.
/// </summary>
public class GameBoardDrawable : IDrawable
{
    private readonly GameConfig _config;
    private GameState? _state;
    public int HighlightBottomRows { get; set; }
    public List<int> HighlightRows { get; set; } = new();
    public bool ShowFallingHighlight { get; set; }
    public bool IsInvisible { get; set; }

    public GameBoardDrawable(GameConfig config)
    {
        _config = config;
    }

    public void UpdateState(GameState state) => _state = state;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_state == null) return;

        float cellSize = _config.CellSize;
        float boardWidth = _config.Columns * cellSize;
        float boardHeight = _config.Rows * cellSize;

        // Center the board in the available space
        float offsetX = (dirtyRect.Width - boardWidth) / 2;
        float offsetY = (dirtyRect.Height - boardHeight) / 2;

        // Draw background
        canvas.FillColor = _config.BoardBackgroundColor;
        canvas.FillRectangle(offsetX, offsetY, boardWidth, boardHeight);

        // Draw grid lines
        canvas.StrokeColor = _config.GridLineColor;
        canvas.StrokeSize = 0.5f;
        for (int col = 0; col <= _config.Columns; col++)
        {
            float x = offsetX + col * cellSize;
            canvas.DrawLine(x, offsetY, x, offsetY + boardHeight);
        }
        for (int row = 0; row <= _config.Rows; row++)
        {
            float y = offsetY + row * cellSize;
            canvas.DrawLine(offsetX, y, offsetX + boardWidth, y);
        }

        // Draw locked cells
        for (int row = 0; row < _config.Rows; row++)
        {
            for (int col = 0; col < _config.Columns; col++)
            {
                var shape = _state.Board.GetCell(row, col);
                if (shape != null)
                {
                    DrawCell(canvas, offsetX, offsetY, row, col, cellSize, _config.PieceColors[shape.Value], 1f);
                }
            }
        }

        // Highlight floating cells during gravity animation
        if (ShowFallingHighlight)
        {
            for (int row = 0; row < _config.Rows - 1; row++)
            {
                for (int col = 0; col < _config.Columns; col++)
                {
                    var shape = _state.Board.GetCell(row, col);
                    if (shape != null)
                    {
                        // Check if ANY cell below in this column is empty
                        // (meaning this cell will eventually fall with gravity)
                        bool isFloating = false;
                        for (int r = row + 1; r < _config.Rows; r++)
                        {
                            if (_state.Board.GetCell(r, col) == null)
                            {
                                isFloating = true;
                                break;
                            }
                        }

                        if (isFloating)
                        {
                            float x = offsetX + col * cellSize + 1;
                            float y = offsetY + row * cellSize + 1;
                            float size = cellSize - 2;

                            // Bright overlay
                            canvas.Alpha = 0.6f;
                            canvas.FillColor = Color.FromRgb(255, 255, 100);
                            canvas.FillRoundedRectangle(x, y, size, size, 2);

                            // Bold white border to make it pop
                            canvas.Alpha = 1f;
                            canvas.StrokeColor = Colors.White;
                            canvas.StrokeSize = 2.5f;
                            canvas.DrawRoundedRectangle(x, y, size, size, 2);
                        }
                    }
                }
            }
        }

        // Draw ghost piece
        if (_state.CurrentPiece != null)
        {
            if (IsInvisible)
            {
                // Invisible ghost: show at deepest valid position with purple tint
                int invisGhostRow = _state.GetInvisibleGhostRow();
                if (invisGhostRow >= 0)
                {
                    DrawPieceWithColor(canvas, offsetX, offsetY, _state.CurrentPiece, invisGhostRow, _state.CurrentCol, cellSize, Color.FromRgba(160, 80, 220, 80));
                }
            }
            else
            {
                int ghostRow = _state.GetGhostRow();
                DrawPiece(canvas, offsetX, offsetY, _state.CurrentPiece, ghostRow, _state.CurrentCol, cellSize, _config.GhostPieceOpacity);
            }
        }

        // Draw current piece
        if (_state.CurrentPiece != null)
        {
            if (IsInvisible)
            {
                // Semi-transparent with a glow effect when cloaked
                DrawPiece(canvas, offsetX, offsetY, _state.CurrentPiece, _state.CurrentRow, _state.CurrentCol, cellSize, 0.4f);
            }
            else
            {
                DrawPiece(canvas, offsetX, offsetY, _state.CurrentPiece, _state.CurrentRow, _state.CurrentCol, cellSize, 1f);
            }
        }

        // Draw border
        canvas.StrokeColor = _config.BoardBorderColor;
        canvas.StrokeSize = _config.BoardBorderWidth;
        canvas.DrawRectangle(offsetX, offsetY, boardWidth, boardHeight);

        // Highlight bottom rows for level-up bonus removal
        if (HighlightBottomRows > 0)
        {
            canvas.Alpha = 0.5f;
            canvas.FillColor = Color.FromRgb(255, 200, 0);
            for (int r = 0; r < HighlightBottomRows; r++)
            {
                int row = _config.Rows - 1 - r;
                float y = offsetY + row * cellSize;
                canvas.FillRectangle(offsetX, y, boardWidth, cellSize);
            }
            canvas.Alpha = 1f;
        }

        // Highlight specific rows being cleared
        if (HighlightRows.Count > 0)
        {
            canvas.Alpha = 0.6f;
            canvas.FillColor = Color.FromRgb(255, 255, 100);
            foreach (int row in HighlightRows)
            {
                float y = offsetY + row * cellSize;
                canvas.FillRectangle(offsetX, y, boardWidth, cellSize);
            }
            canvas.Alpha = 1f;
        }
    }

    private void DrawPiece(ICanvas canvas, float offsetX, float offsetY, Tetromino piece, int pieceRow, int pieceCol, float cellSize, float opacity)
    {
        var matrix = piece.CurrentMatrix;
        var color = _config.PieceColors[piece.Shape];
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (matrix[r, c] == 0) continue;

                int visibleRow = pieceRow + r - _config.BufferRows;
                int visibleCol = pieceCol + c;

                if (visibleRow >= 0 && visibleRow < _config.Rows)
                {
                    DrawCell(canvas, offsetX, offsetY, visibleRow, visibleCol, cellSize, color, opacity);
                }
            }
        }
    }

    private static void DrawCell(ICanvas canvas, float offsetX, float offsetY, int row, int col, float cellSize, Color color, float opacity)
    {
        float x = offsetX + col * cellSize + 1;
        float y = offsetY + row * cellSize + 1;
        float size = cellSize - 2;

        canvas.Alpha = opacity;
        canvas.FillColor = color;
        canvas.FillRoundedRectangle(x, y, size, size, 2);

        // Highlight effect
        canvas.Alpha = opacity * 0.3f;
        canvas.FillColor = Colors.White;
        canvas.FillRoundedRectangle(x, y, size, size / 3, 2);

        canvas.Alpha = 1f;
    }

    private void DrawPieceWithColor(ICanvas canvas, float offsetX, float offsetY, Tetromino piece, int pieceRow, int pieceCol, float cellSize, Color color)
    {
        var matrix = piece.CurrentMatrix;
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (matrix[r, c] == 0) continue;

                int visibleRow = pieceRow + r - _config.BufferRows;
                int visibleCol = pieceCol + c;

                if (visibleRow >= 0 && visibleRow < _config.Rows)
                {
                    float x = offsetX + visibleCol * cellSize + 1;
                    float y = offsetY + visibleRow * cellSize + 1;
                    float size = cellSize - 2;

                    canvas.FillColor = color;
                    canvas.FillRoundedRectangle(x, y, size, size, 2);

                    // Purple glow border
                    canvas.StrokeColor = Color.FromRgba(180, 100, 255, 180);
                    canvas.StrokeSize = 2f;
                    canvas.DrawRoundedRectangle(x, y, size, size, 2);
                }
            }
        }
    }
}
