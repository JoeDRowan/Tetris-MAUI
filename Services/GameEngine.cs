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
    private DateTime _lastSpacePress;
    private const double DoubleTapThresholdMs = 300;

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
        _lastSpacePress = DateTime.MinValue;

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
            int savedRow = _state.CurrentRow;
            int savedCol = _state.CurrentCol;

            var newPiece = new Tetromino(holdShape);
            var matrix = newPiece.CurrentMatrix;

            // Swap only allowed if the held piece fits at the current position
            if (_state.Board.HasCollision(matrix, savedRow, savedCol))
                return;

            _state.HoldPiece = new Tetromino(currentShape);
            _state.CurrentPiece = newPiece;
            _state.CurrentRow = savedRow;
            _state.CurrentCol = savedCol;
        }
        else
        {
            _state.HoldPiece = new Tetromino(currentShape);
            _state.SpawnNextPiece();
        }

        _state.IsInvisible = false;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Handle spacebar press. Detects double-tap for invisibility toggle.
    /// In Classic mode: single tap = Hold, double-tap = toggle invisible.
    /// In Pro mode: double-tap = toggle invisible (single tap does nothing).
    /// Returns "hold" if single-tap hold should execute, "invisible" if toggled, or "none".
    /// </summary>
    public string SpacePressed()
    {
        if (!CanAct()) return "none";

        var now = DateTime.Now;
        double elapsed = (now - _lastSpacePress).TotalMilliseconds;
        _lastSpacePress = now;

        if (elapsed <= DoubleTapThresholdMs)
        {
            // Double-tap detected — toggle invisibility
            ToggleInvisibility();
            return "invisible";
        }

        // For Classic mode, we need to wait to see if it's a double-tap.
        // We'll handle the single-tap case via a delayed action from the UI.
        // For Pro mode (no hold), single tap does nothing.
        if (!_config.HoldEnabled)
        {
            return "none"; // Pro: single tap does nothing, wait for potential double-tap
        }

        return "pending_hold"; // Classic: might be hold, wait for double-tap window
    }

    /// <summary>
    /// Execute hold after confirming it's not a double-tap (called by UI after timeout).
    /// </summary>
    public void ExecuteHold()
    {
        HoldPiece();
    }

    /// <summary>
    /// Toggle invisibility cloak on the current piece.
    /// </summary>
    public void ToggleInvisibility()
    {
        if (!CanAct() || _state.CurrentPiece == null) return;

        if (_state.IsInvisible)
        {
            // Turning OFF invisibility — snap piece to nearest valid position
            _state.IsInvisible = false;
            var matrix = _state.CurrentPiece.CurrentMatrix;

            // If current position is valid, stay there
            if (!_state.Board.HasCollision(matrix, _state.CurrentRow, _state.CurrentCol))
            {
                StateChanged?.Invoke();
                return;
            }

            // Current position overlaps — find the nearest valid position above
            for (int testRow = _state.CurrentRow - 1; testRow >= 0; testRow--)
            {
                if (!_state.Board.HasCollision(matrix, testRow, _state.CurrentCol))
                {
                    _state.CurrentRow = testRow;
                    StateChanged?.Invoke();
                    return;
                }
            }

            // Nowhere valid — lock at invisible ghost position
            int ghostRow = _state.GetInvisibleGhostRow();
            if (ghostRow >= 0)
            {
                _state.CurrentRow = ghostRow;
            }
            StateChanged?.Invoke();
        }
        else
        {
            // Turning ON invisibility
            _state.IsInvisible = true;
            StateChanged?.Invoke();
        }
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

        if (_state.IsInvisible)
        {
            // In invisible mode, piece passes through blocks — only stop at deepest valid fit
            var matrix = _state.CurrentPiece.CurrentMatrix;
            int pieceRows = matrix.GetLength(0);
            int nextRow = _state.CurrentRow + 1;

            // Check if we'd go below the board
            bool hitsFloor = (nextRow + pieceRows) > _state.Board.TotalRows;

            // Find the deepest valid landing position
            int ghostRow = _state.GetInvisibleGhostRow();

            if (hitsFloor)
            {
                // Hit absolute floor — lock at ghost position if valid, else deactivate
                if (ghostRow >= 0)
                {
                    _state.CurrentRow = ghostRow;
                }
                _state.IsInvisible = false;
                LockCurrentPiece();
            }
            else if (ghostRow < 0)
            {
                // No valid position exists anywhere — just keep falling until floor
                _state.CurrentRow = nextRow;
            }
            else if (ghostRow < _state.CurrentRow)
            {
                // Ghost is above us (we've passed it) — shouldn't happen, just fall
                _state.CurrentRow = nextRow;
            }
            else
            {
                // Ghost is below or at current position — fall toward it
                _state.CurrentRow = nextRow;

                // Lock only when we've reached the ghost position
                if (_state.CurrentRow >= ghostRow)
                {
                    _state.CurrentRow = ghostRow;
                    _state.IsInvisible = false;
                    LockCurrentPiece();
                }
            }
        }
        else
        {
            if (!_state.Board.HasCollision(_state.CurrentPiece.CurrentMatrix, _state.CurrentRow + 1, _state.CurrentCol))
            {
                _state.CurrentRow++;
            }
            else
            {
                LockCurrentPiece();
            }
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

        // Clear current piece immediately so it's no longer rendered as a falling piece
        // (its cells are now part of the grid)
        _state.CurrentPiece = null;

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
            _state.CascadeLines += cascadeCleared;
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
            // Clear held piece on level change
            _state.HoldPiece = null;

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
        // Reset invisibility for next piece
        _state.IsInvisible = false;

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

        if (_state.IsInvisible)
        {
            // When invisible, only check wall boundaries, not block collisions
            var matrix = _state.CurrentPiece.CurrentMatrix;
            int cols = matrix.GetLength(1);
            int rows = matrix.GetLength(0);

            // Check horizontal bounds
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (matrix[r, c] == 0) continue;
                    int boardCol = newCol + c;
                    int boardRow = newRow + r;
                    if (boardCol < 0 || boardCol >= _state.Board.Columns) return false;
                    if (boardRow >= _state.Board.TotalRows) return false;
                }
            }

            _state.CurrentRow = newRow;
            _state.CurrentCol = newCol;
            StateChanged?.Invoke();
            return true;
        }

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

        if (_state.IsInvisible)
        {
            // When invisible, only check wall/floor boundaries
            if (!HasBoundaryCollision(newMatrix, _state.CurrentRow, _state.CurrentCol))
            {
                piece.RotationState = newRotation;
                StateChanged?.Invoke();
                return;
            }
            // Try basic wall kicks for boundary only
            int[][] offsets = { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 } };
            foreach (var off in offsets)
            {
                if (!HasBoundaryCollision(newMatrix, _state.CurrentRow + off[1], _state.CurrentCol + off[0]))
                {
                    piece.RotationState = newRotation;
                    _state.CurrentCol += off[0];
                    _state.CurrentRow += off[1];
                    StateChanged?.Invoke();
                    return;
                }
            }
            return;
        }

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

    /// <summary>
    /// Check only wall and floor boundaries (not block collisions). Used for invisible mode.
    /// </summary>
    private bool HasBoundaryCollision(int[,] matrix, int pieceRow, int pieceCol)
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
                if (boardCol < 0 || boardCol >= _state.Board.Columns) return true;
                if (boardRow >= _state.Board.TotalRows) return true;
            }
        }
        return false;
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
