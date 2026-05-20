namespace Tetris.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tetris.Config;
using Tetris.Models;
using Tetris.Services;

public class GameViewModel : INotifyPropertyChanged
{
    private readonly GameEngine _engine;
    private readonly HighScoreService _highScoreService;

    public GameConfig Config => _engine.Config;
    public GameState State => _engine.State;
    public HighScoreService HighScoreService => _highScoreService;
    public TimeSpan ElapsedTime => _engine.ElapsedTime;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Redraw;
    public event Action? OnGameOver;
    public event Action<int>? OnLinesCleared;
    public event Action<int>? OnLevelUp;
    public event Action<int, int>? OnLevelUpRowsRemoved;

    public int Score => State.Score;
    public int Level => State.Level;
    public int Lines => State.TotalLinesCleared;
    public bool IsGameOver => State.IsGameOver;
    public bool IsPaused => State.IsPaused;
    public string StatusText => State.IsGameOver ? "GAME OVER" : State.IsPaused ? "PAUSED" : "";

    public GameViewModel()
    {
        var config = new GameConfig();
        _engine = new GameEngine(config);
        _highScoreService = new HighScoreService();

        _engine.StateChanged += () =>
        {
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(Lines));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsGameOver));
            OnPropertyChanged(nameof(IsPaused));
            Redraw?.Invoke();
        };

        _engine.GameOver += () =>
        {
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsGameOver));
            OnGameOver?.Invoke();
        };

        _engine.LinesCleared += (lines) => OnLinesCleared?.Invoke(lines);
        _engine.LevelUp += (level) => OnLevelUp?.Invoke(level);
        _engine.LevelUpRowsRemoved += (level, rows) => OnLevelUpRowsRemoved?.Invoke(level, rows);
    }

    public void SetMode(GameMode mode) => Config.Mode = mode;
    public void StartNewGame(IDispatcher dispatcher) => _engine.StartNewGame(dispatcher);
    public void EndGame() => _engine.EndGame();
    public void TogglePause() => _engine.TogglePause();
    public void MoveLeft() => _engine.MoveLeft();
    public void MoveRight() => _engine.MoveRight();
    public void RotateClockwise() => _engine.RotateClockwise();
    public void DownPress() => _engine.DownPress();
    public void StopSoftDrop() => _engine.StopSoftDrop();
    public void HoldPiece() => _engine.HoldPiece();

    public bool CheckHighScore() => _highScoreService.IsHighScore(State.Score);

    public int SaveHighScore(string name)
    {
        return _highScoreService.AddScore(new HighScore
        {
            Name = name,
            Score = State.Score,
            Level = State.Level,
            Lines = State.TotalLinesCleared,
            Date = DateTime.Now
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
