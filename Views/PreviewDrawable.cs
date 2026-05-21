namespace Tetris.Views;

using Microsoft.Maui.Graphics;
using Tetris.Config;
using Tetris.Models;

/// <summary>
/// Renders the next pieces and hold piece panels.
/// </summary>
public class PreviewDrawable : IDrawable
{
    private readonly GameConfig _config;
    private readonly bool _isHoldPanel;
    private Tetromino? _holdPiece;
    private TetrominoShape[]? _nextPieces;

    public PreviewDrawable(GameConfig config, bool isHoldPanel)
    {
        _config = config;
        _isHoldPanel = isHoldPanel;
    }

    public void UpdateHold(Tetromino? holdPiece) => _holdPiece = holdPiece;
    public void UpdateNext(TetrominoShape[] nextPieces) => _nextPieces = nextPieces;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cellSize = _isHoldPanel ? _config.CellSize * 0.7f : _config.CellSize * 0.7f;
        float padding = _isHoldPanel ? 10f : 4f;

        // Background
        canvas.FillColor = _config.BoardBackgroundColor;
        canvas.FillRoundedRectangle(0, 0, dirtyRect.Width, dirtyRect.Height, 4);
        canvas.StrokeColor = _config.BoardBorderColor;
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(0, 0, dirtyRect.Width, dirtyRect.Height, 4);

        if (_isHoldPanel)
        {
            DrawSinglePiece(canvas, dirtyRect, _holdPiece?.Shape, cellSize, padding);
        }
        else if (_nextPieces != null)
        {
            // Calculate total height using actual occupied rows (skip empty top/bottom rows)
            float totalHeight = 0;
            var pieceBounds = new List<(TetrominoShape shape, int[,] matrix, int topRow, int bottomRow)>();
            foreach (var shape in _nextPieces)
            {
                var matrix = TetrominoData.GetRotations(shape)[0];
                int rows = matrix.GetLength(0);
                int cols = matrix.GetLength(1);
                int topRow = 0, bottomRow = rows - 1;
                // Find first occupied row
                for (int r = 0; r < rows; r++)
                {
                    bool hasCell = false;
                    for (int c = 0; c < cols; c++) { if (matrix[r, c] != 0) { hasCell = true; break; } }
                    if (hasCell) { topRow = r; break; }
                }
                // Find last occupied row
                for (int r = rows - 1; r >= 0; r--)
                {
                    bool hasCell = false;
                    for (int c = 0; c < cols; c++) { if (matrix[r, c] != 0) { hasCell = true; break; } }
                    if (hasCell) { bottomRow = r; break; }
                }
                int occupiedRows = bottomRow - topRow + 1;
                totalHeight += occupiedRows * cellSize;
                pieceBounds.Add((shape, matrix, topRow, bottomRow));
            }
            totalHeight += (pieceBounds.Count - 1) * padding;

            float yOffset = (dirtyRect.Height - totalHeight) / 2;
            foreach (var (shape, matrix, topRow, bottomRow) in pieceBounds)
            {
                // Offset drawing by skipping empty top rows
                float adjustedY = yOffset - topRow * cellSize;
                int occupiedRows = bottomRow - topRow + 1;
                DrawMatrix(canvas, matrix, shape, dirtyRect.Width, adjustedY, cellSize);
                yOffset += occupiedRows * cellSize + padding;
            }
        }
    }

    private void DrawSinglePiece(ICanvas canvas, RectF bounds, TetrominoShape? shape, float cellSize, float padding)
    {
        if (shape == null) return;

        var matrix = TetrominoData.GetRotations(shape.Value)[0];
        float yOffset = (bounds.Height - matrix.GetLength(0) * cellSize) / 2;
        DrawMatrix(canvas, matrix, shape.Value, bounds.Width, yOffset, cellSize);
    }

    private void DrawMatrix(ICanvas canvas, int[,] matrix, TetrominoShape shape, float panelWidth, float yOffset, float cellSize)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        float xOffset = (panelWidth - cols * cellSize) / 2;
        var color = _config.PieceColors[shape];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (matrix[r, c] == 0) continue;

                float x = xOffset + c * cellSize + 1;
                float y = yOffset + r * cellSize + 1;
                float size = cellSize - 2;

                canvas.Alpha = 1f;
                canvas.FillColor = color;
                canvas.FillRoundedRectangle(x, y, size, size, 2);
            }
        }
    }
}
