namespace Tetris.Services;

using Tetris.Config;
using Tetris.Models;

/// <summary>
/// Core game engine handling the game loop, input, and state transitions.
/// </summary>
public class GameEngine
{
    private readonly GameConfig _config;
    private readonly GameState _state;
    private readonly ScoreService _scoreService;

    private IDispatcherTimer? _gameTimer;
    private double _dropAccumulator;
    private double _currentDropInterval;
    private bool _softDropping;

    public GameState State => _state;
    public GameConfig Config => _config;

    public event Action? StateChanged;
    public event Action? GameOver;
    public event Action<int>? LinesCleared;

    public GameEngine(GameConfig config)
    {
        _config = config;
        _state = new GameState(config);
        _scoreService = new ScoreService(config);
    }

    public void StartNewGame(IDispatcher dispatcher)
    {
        _state.Initialize();
        _currentDropInterval = _config.GetDropIntervalMs(_state.Level);
        _dropAccumulator = 0;
        _softDropping = false;

        _gameTimer?.Stop();
        _gameTimer = dispatcher.CreateTimer();
        _gameTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps tick
        _gameTimer.Tick += OnTick;
        _gameTimer.Start();

        StateChanged?.Invoke();
    }

    public void TogglePause()
    {
        if (_state.IsGameOver) return;
        _state.IsPaused = !_state.IsPaused;
        StateChanged?.Invoke();
    }

    public void MoveLeft()
    {
        if (!CanAct()) return;
        TryMove(0, -1);
    }

    public void MoveRight()
    {
        if (!CanAct()) return;
        TryMove(0, 1);
    }

    public void RotateClockwise()
    {
        if (!CanAct() || _state.CurrentPiece == null) return;
        TryRotate(clockwise: true);
    }

    public void RotateCounterClockwise()
    {
        if (!CanAct() || _state.CurrentPiece == null) return;
        TryRotate(clockwise: false);
    }

    public void StartSoftDrop()
    {
        if (!CanAct()) return;
        _softDropping = true;
    }

    public void StopSoftDrop()
    {
        _softDropping = false;
    }

    public void HoldPiece()
    {
        if (!CanAct() || _state.CurrentPiece == null || _state.HoldUsedThisTurn) return;

        _state.HoldUsedThisTurn = true;
        var currentShape = _state.CurrentPiece.Shape;

        if (_state.HoldPiece != null)
        {
            var holdShape = _state.HoldPiece.Shape;
            _state.HoldPiece = new Tetromino(currentShape);

            _state.CurrentPiece = new Tetromino(holdShape);
            var matrix = _state.CurrentPiece.CurrentMatrix;
            _state.CurrentCol = (_config.Columns - matrix.GetLength(1)) / 2;
            _state.CurrentRow = _config.BufferRows - matrix.GetLength(0);
        }
        else
        {
            _state.HoldPiece = new Tetromino(currentShape);
            _state.SpawnNextPiece();
        }

        StateChanged?.Invoke();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_state.IsPaused || _state.IsGameOver) return;

        double interval = _softDropping
            ? _currentDropInterval / _config.SoftDropSpeedMultiplier
            : _currentDropInterval;

        _dropAccumulator += 16; // Approximate ms per tick

        if (_dropAccumulator >= interval)
        {
            _dropAccumulator = 0;
            DropOneLine();
        }
    }

    private void DropOneLine()
    {
        if (_state.CurrentPiece == null) return;

        if (!_state.Board.HasCollision(_state.CurrentPiece.CurrentMatrix, _state.CurrentRow + 1, _state.CurrentCol))
        {
            _state.CurrentRow++;
        }
        else
        {
            LockCurrentPiece();
        }

        StateChanged?.Invoke();
    }

    private void LockCurrentPiece()
    {
        if (_state.CurrentPiece == null) return;

        _state.Board.LockPiece(_state.CurrentPiece.Shape, _state.CurrentPiece.CurrentMatrix, _state.CurrentRow, _state.CurrentCol);

        int cleared = _state.Board.ClearLines();
        if (cleared > 0)
        {
            _state.AddLinesCleared(cleared);
            _currentDropInterval = _config.GetDropIntervalMs(_state.Level);
            LinesCleared?.Invoke(cleared);
        }

        // Check game over
        if (_state.Board.IsBufferOccupied())
        {
            _state.IsGameOver = true;
            _gameTimer?.Stop();
            GameOver?.Invoke();
            return;
        }

        if (!_state.SpawnNextPiece())
        {
            _gameTimer?.Stop();
            GameOver?.Invoke();
            return;
        }

        _dropAccumulator = 0;
    }

    private bool TryMove(int dRow, int dCol)
    {
        if (_state.CurrentPiece == null) return false;

        int newRow = _state.CurrentRow + dRow;
        int newCol = _state.CurrentCol + dCol;

        if (!_state.Board.HasCollision(_state.CurrentPiece.CurrentMatrix, newRow, newCol))
        {
            _state.CurrentRow = newRow;
            _state.CurrentCol = newCol;
            StateChanged?.Invoke();
            return true;
        }
        return false;
    }

    private void TryRotate(bool clockwise)
    {
        if (_state.CurrentPiece == null) return;

        var piece = _state.CurrentPiece;
        int fromRotation = piece.RotationState;
        int newRotation = clockwise
            ? (fromRotation + 1) % piece.RotationCount
            : (fromRotation - 1 + piece.RotationCount) % piece.RotationCount;

        var newMatrix = piece.GetRotation(newRotation);

        // Get wall kick offsets
        var kicks = GetWallKicks(piece.Shape, fromRotation, clockwise);

        foreach (var (dx, dy) in kicks)
        {
            int testCol = _state.CurrentCol + dx;
            int testRow = _state.CurrentRow - dy; // Y is inverted (up is negative)

            if (!_state.Board.HasCollision(newMatrix, testRow, testCol))
            {
                piece.RotationState = newRotation;
                _state.CurrentCol = testCol;
                _state.CurrentRow = testRow;
                StateChanged?.Invoke();
                return;
            }
        }
    }

    private static (int dx, int dy)[] GetWallKicks(TetrominoShape shape, int fromRotation, bool clockwise)
    {
        if (shape == TetrominoShape.O)
            return [(0, 0)];

        if (shape == TetrominoShape.I)
            return clockwise ? TetrominoData.WallKicksICW[fromRotation] : TetrominoData.WallKicksICCW[fromRotation];

        return clockwise ? TetrominoData.WallKicksCW[fromRotation] : TetrominoData.WallKicksCCW[fromRotation];
    }

    private bool CanAct() => !_state.IsPaused && !_state.IsGameOver && _state.CurrentPiece != null;
}
