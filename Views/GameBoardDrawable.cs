namespace Tetris.Views;

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

        // Draw ghost piece
        if (_state.CurrentPiece != null)
        {
            int ghostRow = _state.GetGhostRow();
            DrawPiece(canvas, offsetX, offsetY, _state.CurrentPiece, ghostRow, _state.CurrentCol, cellSize, _config.GhostPieceOpacity);
        }

        // Draw current piece
        if (_state.CurrentPiece != null)
        {
            DrawPiece(canvas, offsetX, offsetY, _state.CurrentPiece, _state.CurrentRow, _state.CurrentCol, cellSize, 1f);
        }

        // Draw border
        canvas.StrokeColor = _config.BoardBorderColor;
        canvas.StrokeSize = _config.BoardBorderWidth;
        canvas.DrawRectangle(offsetX, offsetY, boardWidth, boardHeight);
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
}
