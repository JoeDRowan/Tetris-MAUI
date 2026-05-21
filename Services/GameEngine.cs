namespace Tetris.Services;

using System.Collections.Generic;
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
    private int _pendingBonusRows;

    public GameState State => _state;
    public GameConfig Config => _config;
    public TimeSpan ElapsedTime => _gameStartTime == default ? TimeSpan.Zero : DateTime.Now - _gameStartTime;

    public event Action? StateChanged;
    public event Action? GameOver;
    public event Action<int>? LinesCleared;
    public event Action<int>? LevelUp;
    public event Action<int, int>? LevelUpRowsRemoved; // (newLevel, rowsRemoved)
    public event Action<int, int>? CascadeClear; // (cascadeLevel, linesCleared)

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
        _pendingBonusRows = 0;
        _gameStartTime = DateTime.Now;

        _gameTimer?.Stop();
        _gameTimer = dispatcher.CreateTimer();
        _gameTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps tick
        _gameTimer.Tick += OnTick;
        _gameTimer.Start();

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Called by UI after level-up bonus animation completes to actually remove the rows.
    /// </summary>
    public int ExecuteLevelBonusRemoval()
    {
        if (_pendingBonusRows <= 0) return 0;
        int removed = _state.Board.RemoveBottomRows(_pendingBonusRows);
        _pendingBonusRows = 0;
        StateChanged?.Invoke();
        return removed;
    }

    public void EndGame()
    {
        if (_state.IsGameOver) return;
        _state.IsGameOver = true;
        _gameTimer?.Stop();
        GameOver?.Invoke();
    }

    public void ContinueGame()
    {
        if (!_state.IsGameOver) return;
        _state.IsGameOver = false;
        _gameTimer?.Start();
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

        // Immediately drop one line on each press for responsiveness
        if (!_fastDropActive)
        {
            DropOneLine();
        }
    }

    public void StopSoftDrop()
    {
        _softDropping = false;
        // Don't reset fast drop here — let it persist until piece locks
    }

    public void HoldPiece()
    {
        if (!_config.HoldEnabled) return;
        if (!CanAct() || _state.CurrentPiece == null) return;

        var currentShape = _state.CurrentPiece.Shape;

        if (_state.HoldPiece != null)
        {
            var holdShape = _state.HoldPiece.Shape;
            _state.HoldPiece = new Tetromino(currentShape);

            _state.CurrentPiece = new Tetromino(holdShape);
            var matrix = _state.CurrentPiece.CurrentMatrix;
            _state.CurrentCol = (_config.Columns - matrix.GetLength(1)) / 2;
            _state.CurrentRow = _config.BufferRows;
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

        // Timeout fast drop if no key press within trigger window
        if (_fastDropActive && (DateTime.Now - _lastDownPress).TotalMilliseconds > _config.FastDropTriggerMs * 2)
        {
            _fastDropActive = false;
            _downPressCount = 0;
        }

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

    private int _previousLevelAtLock;

    /// <summary>
    /// Event fired when rows are about to be cleared. Contains visible row indices and whether it's a cascade.
    /// UI should highlight these, then call ExecutePendingClear() or ExecutePendingCascadeClear().
    /// </summary>
    public event Action<List<int>, bool>? PendingLineClear;

    private void LockCurrentPiece()
    {
        if (_state.CurrentPiece == null) return;

        _state.Board.LockPiece(_state.CurrentPiece.Shape, _state.CurrentPiece.CurrentMatrix, _state.CurrentRow, _state.CurrentCol);

        _previousLevelAtLock = _state.Level;
        var fullRows = _state.Board.GetFullRowIndices();

        if (fullRows.Count > 0)
        {
            // Pause game while UI highlights the rows
            _gameTimer?.Stop();
            PendingLineClear?.Invoke(fullRows, false);
            // UI will call ExecutePendingClear() after highlight animation
        }
        else
        {
            FinalizeLock();
        }
    }

    /// <summary>
    /// Event fired after lines are cleared, signaling UI to animate gravity.
    /// UI should call ApplyGravityStep repeatedly, then CheckForCascade when done.
    /// </summary>
    public event Action? GravityNeeded;

    /// <summary>
    /// Called by UI after row highlight animation completes.
    /// Clears the rows and signals gravity is needed.
    /// </summary>
    public void ExecutePendingClear()
    {
        int cleared = _state.Board.ClearLines();
        if (cleared > 0)
        {
            _state.AddLinesCleared(cleared);
            _currentDropInterval = _config.GetDropIntervalMs(_state.Level);
            LinesCleared?.Invoke(cleared);
        }

        StateChanged?.Invoke();
        GravityNeeded?.Invoke();
    }

    /// <summary>
    /// Called by UI after cascade row highlight animation completes.
    /// </summary>
    public void ExecutePendingCascadeClear()
    {
        int cascadeCleared = _state.Board.ClearLines();
        if (cascadeCleared > 0)
        {
            _state.CascadeCount++;
            _state.AddLinesCleared(cascadeCleared);
            _currentDropInterval = _config.GetDropIntervalMs(_state.Level);
            CascadeClear?.Invoke(_state.CascadeCount, cascadeCleared);
        }

        StateChanged?.Invoke();
        GravityNeeded?.Invoke();
    }

    /// <summary>
    /// Drops all floating cells by one row. Returns true if any moved.
    /// </summary>
    public bool ApplyGravityStep()
    {
        bool moved = _state.Board.ApplyGravityOneStep();
        if (moved) StateChanged?.Invoke();
        return moved;
    }

    /// <summary>
    /// Check if any cells would fall. Does NOT modify board.
    /// </summary>
    public bool HasFloatingCells() => _state.Board.HasFloatingCells();

    /// <summary>
    /// Called by UI after gravity animation completes. Checks for cascade rows.
    /// </summary>
    public void CheckForCascade()
    {
        var cascadeRows = _state.Board.GetFullRowIndices();
        if (cascadeRows.Count > 0)
        {
            PendingLineClear?.Invoke(cascadeRows, true);
        }
        else
        {
            FinalizeLockAfterClears();
        }
    }

    private void FinalizeLockAfterClears()
    {
        if (_state.Level > _previousLevelAtLock)
        {
            LevelUp?.Invoke(_state.Level);

            int rowsToRemove = _state.Level / _config.LevelUpRowDivisor;
            if (rowsToRemove > 0)
            {
                _pendingBonusRows = rowsToRemove;
                LevelUpRowsRemoved?.Invoke(_state.Level, rowsToRemove);
            }
        }

        FinalizeLock();
    }

    private void FinalizeLock()
    {
        // Check game over
        if (_state.Board.IsBufferOccupied())
        {
            _state.IsGameOver = true;
            GameOver?.Invoke();
            return;
        }

        if (!_state.SpawnNextPiece())
        {
            GameOver?.Invoke();
            return;
        }

        // Reset fast drop
        _fastDropActive = false;
        _downPressCount = 0;
        _dropAccumulator = 0;

        // Resume game timer
        _gameTimer?.Start();
        StateChanged?.Invoke();
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
