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
    private DateTime _lastDownPress;
    private int _downPressCount;
    private bool _fastDropActive;
    private DateTime _gameStartTime;

    public GameState State => _state;
    public GameConfig Config => _config;
    public TimeSpan ElapsedTime => _gameStartTime == default ? TimeSpan.Zero : DateTime.Now - _gameStartTime;

    public event Action? StateChanged;
    public event Action? GameOver;
    public event Action<int>? LinesCleared;
    public event Action<int>? LevelUp;

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
        _fastDropActive = false;
        _downPressCount = 0;
        _gameStartTime = DateTime.Now;

        _gameTimer?.Stop();
        _gameTimer = dispatcher.CreateTimer();
        _gameTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps tick
        _gameTimer.Tick += OnTick;
        _gameTimer.Start();

        StateChanged?.Invoke();
    }

    public void EndGame()
    {
        if (_state.IsGameOver) return;
        _state.IsGameOver = true;
        _gameTimer?.Stop();
        GameOver?.Invoke();
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

    /// <summary>
    /// Called on each down-key press. Tracks rapid presses for fast drop.
    /// </summary>
    public void DownPress()
    {
        if (!CanAct()) return;

        var now = DateTime.Now;
        if ((now - _lastDownPress).TotalMilliseconds < _config.FastDropTriggerMs)
        {
            _downPressCount++;
            if (_downPressCount >= 3)
            {
                _fastDropActive = true;
            }
        }
        else
        {
            _downPressCount = 1;
            _fastDropActive = false;
        }
        _lastDownPress = now;

        _softDropping = true;
    }

    public void StopSoftDrop()
    {
        _softDropping = false;
        _fastDropActive = false;
        _downPressCount = 0;
    }

    public void HoldPiece()
    {
        if (!_config.HoldEnabled) return;
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
            _state.CurrentRow = 0;
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

        double interval;
        if (_fastDropActive)
        {
            interval = _config.FastDropIntervalMs;
        }
        else if (_softDropping)
        {
            interval = _currentDropInterval / _config.SoftDropSpeedMultiplier;
        }
        else
        {
            interval = _currentDropInterval;
        }

        _dropAccumulator += 16;

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

        int previousLevel = _state.Level;
        int cleared = _state.Board.ClearLines();
        if (cleared > 0)
        {
            _state.AddLinesCleared(cleared);
            _currentDropInterval = _config.GetDropIntervalMs(_state.Level);
            LinesCleared?.Invoke(cleared);

            if (_state.Level > previousLevel)
            {
                LevelUp?.Invoke(_state.Level);
            }
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
        _fastDropActive = false;
        _downPressCount = 0;
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

        var kicks = GetWallKicks(piece.Shape, fromRotation, clockwise);

        foreach (var (dx, dy) in kicks)
        {
            int testCol = _state.CurrentCol + dx;
            int testRow = _state.CurrentRow - dy;

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
