namespace Tetris.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tetris.Config;
using Tetris.Models;
using Tetris.Services;

/// <summary>
/// ViewModel bridging the game engine to the UI.
/// </summary>
public class GameViewModel : INotifyPropertyChanged
{
    private readonly GameEngine _engine;
    private readonly HighScoreService _highScoreService;

    public GameConfig Config => _engine.Config;
    public GameState State => _engine.State;
    public HighScoreService HighScoreService => _highScoreService;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Redraw;
    public event Action? OnGameOver;

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
    }

    public void StartNewGame(IDispatcher dispatcher) => _engine.StartNewGame(dispatcher);
    public void TogglePause() => _engine.TogglePause();
    public void MoveLeft() => _engine.MoveLeft();
    public void MoveRight() => _engine.MoveRight();
    public void RotateClockwise() => _engine.RotateClockwise();
    public void RotateCounterClockwise() => _engine.RotateCounterClockwise();
    public void StartSoftDrop() => _engine.StartSoftDrop();
    public void StopSoftDrop() => _engine.StopSoftDrop();
    public void HardDrop() => _engine.HardDrop();
    public void HoldPiece() => _engine.HoldPiece();

    public bool CheckHighScore() => _highScoreService.IsHighScore(State.Score);

    public void SaveHighScore(string name)
    {
        _highScoreService.AddScore(new HighScore
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
