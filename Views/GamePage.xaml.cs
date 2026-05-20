namespace Tetris.Views;

using Tetris.Config;
using Tetris.ViewModels;

public partial class GamePage : ContentPage
{
    private readonly GameViewModel _viewModel;
    private readonly GameBoardDrawable _boardDrawable;
    private readonly PreviewDrawable _nextDrawable;
    private readonly PreviewDrawable _holdDrawable;
    private bool _gameActive;
    private bool _dialogOpen;

    public GamePage()
    {
        InitializeComponent();

        _viewModel = new GameViewModel();
        BindingContext = _viewModel;

        _boardDrawable = new GameBoardDrawable(_viewModel.Config);
        _nextDrawable = new PreviewDrawable(_viewModel.Config, isHoldPanel: false);
        _holdDrawable = new PreviewDrawable(_viewModel.Config, isHoldPanel: true);

        BoardView.Drawable = _boardDrawable;
        NextView.Drawable = _nextDrawable;
        HoldView.Drawable = _holdDrawable;

        BoardView.WidthRequest = _viewModel.Config.Columns * _viewModel.Config.CellSize + 4;
        BoardView.HeightRequest = _viewModel.Config.Rows * _viewModel.Config.CellSize + 4;

        _viewModel.Redraw += OnRedraw;
        _viewModel.OnGameOver += OnGameOver;
        _viewModel.OnLinesCleared += OnLinesCleared;
        _viewModel.OnLevelUp += OnLevelUp;
        _viewModel.PropertyChanged += (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ScoreLabel.Text = _viewModel.Score.ToString();
                LevelLabel.Text = _viewModel.Level.ToString();
                LinesLabel.Text = _viewModel.Lines.ToString();
                StatusLabel.Text = _viewModel.StatusText;
                StatusLabel.IsVisible = !string.IsNullOrEmpty(_viewModel.StatusText);
            });
        };
    }

    private void OnModeToggled(object? sender, ToggledEventArgs e)
    {
        bool isPro = e.Value;
        _viewModel.SetMode(isPro ? GameMode.Pro : GameMode.Classic);
        ClassicLabel.TextColor = isPro ? Color.FromRgb(136, 136, 136) : Color.FromRgb(32, 96, 204);
        ClassicLabel.FontAttributes = isPro ? FontAttributes.None : FontAttributes.Bold;
        ProLabel.TextColor = isPro ? Color.FromRgb(204, 50, 50) : Color.FromRgb(136, 136, 136);
        ProLabel.FontAttributes = isPro ? FontAttributes.Bold : FontAttributes.None;
        HoldLabel.IsVisible = !isPro;
        HoldView.IsVisible = !isPro;

        // Color scheme change for Pro mode
        if (isPro)
        {
            RightPanel.BackgroundColor = Color.FromRgb(230, 200, 200);
            RightPanel.Stroke = Color.FromRgb(180, 100, 100);
            ModeArea.BackgroundColor = Color.FromRgb(250, 220, 220);
            ModeArea.Stroke = Color.FromRgb(180, 100, 100);
        }
        else
        {
            RightPanel.BackgroundColor = Color.FromRgb(184, 200, 232);
            RightPanel.Stroke = Color.FromRgb(119, 153, 187);
            ModeArea.BackgroundColor = Color.FromRgb(221, 230, 248);
            ModeArea.Stroke = Color.FromRgb(119, 153, 204);
        }
    }

    private void OnNewGameClicked(object? sender, EventArgs e)
    {
        InfoPanel.IsVisible = false;
        StatsPanel.IsVisible = false;
        NewGameButton.IsVisible = true;
        ExitGameButton.IsVisible = true;
        _gameActive = true;
        _viewModel.StartNewGame(Dispatcher);
    }

    private void OnExitGameClicked(object? sender, EventArgs e)
    {
        if (_gameActive)
        {
            _viewModel.EndGame();
        }
    }

    private void OnStatsExitClicked(object? sender, EventArgs e)
    {
        Application.Current?.CloseWindow(Application.Current.Windows[0]);
    }

    private void OnRedraw()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _boardDrawable.UpdateState(_viewModel.State);
            _nextDrawable.UpdateNext([.. _viewModel.State.NextQueue.Take(_viewModel.Config.PreviewCount)]);
            _holdDrawable.UpdateHold(_viewModel.State.HoldPiece);

            BoardView.Invalidate();
            NextView.Invalidate();
            HoldView.Invalidate();
        });
    }

    private void OnLinesCleared(int lines)
    {
        if (lines >= 4)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                StatusLabel.Text = "TETRIS!";
                StatusLabel.TextColor = Color.FromRgb(220, 50, 50);
                StatusLabel.FontSize = 40;
                StatusLabel.IsVisible = true;

                await Task.Delay((int)_viewModel.Config.TetrisOverlayDurationMs);

                if (StatusLabel.Text == "TETRIS!")
                {
                    StatusLabel.IsVisible = false;
                    StatusLabel.TextColor = Color.FromRgb(34, 34, 34);
                    StatusLabel.FontSize = 32;
                }
            });
        }
    }

    private void OnLevelUp(int newLevel)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            StatusLabel.Text = $"LEVEL {newLevel}\nSpeeding up!";
            StatusLabel.TextColor = Color.FromRgb(32, 96, 204);
            StatusLabel.FontSize = 28;
            StatusLabel.IsVisible = true;

            // Flash effect
            await Task.Delay(200);
            StatusLabel.Opacity = 0.5;
            await Task.Delay(150);
            StatusLabel.Opacity = 1.0;
            await Task.Delay(150);
            StatusLabel.Opacity = 0.5;
            await Task.Delay(150);
            StatusLabel.Opacity = 1.0;
            await Task.Delay((int)_viewModel.Config.LevelUpFlashDurationMs - 650);

            if (StatusLabel.Text?.StartsWith("LEVEL") == true)
            {
                StatusLabel.IsVisible = false;
                StatusLabel.TextColor = Color.FromRgb(34, 34, 34);
                StatusLabel.FontSize = 32;
                StatusLabel.Opacity = 1.0;
            }
        });
    }

    private void OnGameOver()
    {
        _gameActive = false;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ExitGameButton.IsVisible = false;
            ShowStatsOverlay();
        });
    }

    private void ShowStatsOverlay()
    {
        var elapsed = _viewModel.ElapsedTime;
        var modeText = _viewModel.Config.Mode == GameMode.Pro ? "Pro" : "Classic";

        StatsScoreLabel.Text = $"Score: {_viewModel.Score}";
        StatsLevelLabel.Text = $"Level: {_viewModel.Level}";
        StatsLinesLabel.Text = $"Lines Cleared: {_viewModel.Lines}";
        StatsTimeLabel.Text = $"Time: {elapsed.Minutes}:{elapsed.Seconds:D2}";
        StatsModeLabel.Text = $"Mode: {modeText}";
        StatsRankLabel.IsVisible = false;
        StatsPanel.IsVisible = true;

        if (_viewModel.CheckHighScore())
        {
            ShowHighScorePrompt();
        }
    }

    private async void ShowHighScorePrompt()
    {
        _dialogOpen = true;
        string name = await DisplayPromptAsync("High Score!",
            $"Score: {_viewModel.Score}\nEnter your name:",
            "Save", "Cancel", "Player", 20);
        _dialogOpen = false;

        if (!string.IsNullOrWhiteSpace(name))
        {
            int rank = _viewModel.SaveHighScore(name);
            if (rank > 0)
            {
                StatsRankLabel.Text = $"🏆 High Score! Ranked #{rank}";
                StatsRankLabel.IsVisible = true;
            }
        }
    }

    public bool HandleKeyDown(string key)
    {
        if (_dialogOpen) return false;

        switch (key.ToLowerInvariant())
        {
            case "left":
            case "a":
                _viewModel.MoveLeft();
                return true;
            case "right":
            case "d":
                _viewModel.MoveRight();
                return true;
            case "up":
            case "w":
                _viewModel.RotateClockwise();
                return true;
            case "down":
            case "s":
                _viewModel.DownPress();
                return true;
            case "space":
                _viewModel.HoldPiece();
                return true;
            case "p":
            case "escape":
                _viewModel.TogglePause();
                return true;
            case "e":
                if (_gameActive) _viewModel.EndGame();
                return true;
            case "n":
                OnNewGameClicked(null, EventArgs.Empty);
                return true;
        }
        return false;
    }

    public bool HandleKeyUp(string key)
    {
        if (_dialogOpen) return false;

        switch (key.ToLowerInvariant())
        {
            case "down":
            case "s":
                _viewModel.StopSoftDrop();
                return true;
        }
        return false;
    }
}
