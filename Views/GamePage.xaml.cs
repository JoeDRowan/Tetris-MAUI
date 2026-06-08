namespace Tetris.Views;

using System.Collections.Generic;
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
    private CancellationTokenSource? _holdDelayCts;

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
        _viewModel.OnLevelUpRowsRemoved += OnLevelUpRowsRemoved;
        _viewModel.OnCascadeClear += OnCascadeClear;
        _viewModel.OnPendingLineClear += OnPendingLineClear;
        _viewModel.OnGravityNeeded += OnGravityNeeded;
        _viewModel.PropertyChanged += (s, e) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ScoreLabel.Text = _viewModel.Score.ToString();
                LevelLabel.Text = _viewModel.Level.ToString();
                SpeedLabel.Text = _viewModel.Speed.ToString("F1");
                LinesLabel.Text = _viewModel.Lines.ToString();
                RemainingLabel.Text = _viewModel.LinesRemaining.ToString();

                if (!string.IsNullOrEmpty(_viewModel.StatusText))
                {
                    StatusLabel.Text = _viewModel.StatusText;
                    StatusBorder.IsVisible = true;
                }
                else if (StatusLabel.Text == "PAUSED" || StatusLabel.Text == "GAME OVER")
                {
                    StatusBorder.IsVisible = false;
                }
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
        HoldBorder.IsVisible = !isPro;

        // If switching to Pro mid-game, clear the held piece
        if (isPro && _gameActive)
        {
            _viewModel.State.HoldPiece = null;
            _holdDrawable.UpdateHold(null);
            HoldView.Invalidate();
        }

        // Grey out "Space Hold" in bottom bar for Pro mode
        HoldKeyLabel.TextColor = isPro ? Color.FromRgb(170, 170, 170) : Color.FromRgb(26, 48, 112);
        HoldKeyBorder.BackgroundColor = isPro ? Color.FromRgb(220, 220, 220) : Color.FromRgb(184, 200, 232);
        HoldKeyBorder.Stroke = isPro ? Color.FromRgb(180, 180, 180) : Color.FromRgb(102, 136, 170);

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
        // Mode switch stays enabled during game
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

    private void OnPlayAgainClicked(object? sender, EventArgs e)
    {
        OnNewGameClicked(sender, e);
    }

    private void OnStatsContinueClicked(object? sender, EventArgs e)
    {
        StatsPanel.IsVisible = false;
        _gameActive = true;
        ExitGameButton.IsVisible = true;
        _viewModel.ResumeGame();
    }

    private void OnRedraw()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _boardDrawable.UpdateState(_viewModel.State);
            _boardDrawable.IsInvisible = _viewModel.IsInvisible;
            _nextDrawable.UpdateNext([.. _viewModel.State.NextQueue.Take(_viewModel.Config.PreviewCount)]);
            _holdDrawable.UpdateHold(_viewModel.State.HoldPiece);

            BoardView.Invalidate();
            NextView.Invalidate();
            HoldView.Invalidate();
        });
    }

    private void OnPendingLineClear(List<int> rowIndices, bool isCascade)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Highlight the rows about to be cleared
            _boardDrawable.HighlightRows = rowIndices;
            BoardView.Invalidate();

            // Hold highlight so player can see which rows are clearing
            await Task.Delay(250);

            // Remove highlight and clear the rows
            _boardDrawable.HighlightRows = new List<int>();
            BoardView.Invalidate();

            // Execute the clear (will fire GravityNeeded)
            if (isCascade)
                _viewModel.ExecutePendingCascadeClear();
            else
                _viewModel.ExecutePendingClear();
        });
    }

    private void OnGravityNeeded()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Check if gravity has anything to do (non-destructive check)
            if (!_viewModel.HasFloatingCells())
            {
                // No floating cells — go straight to cascade check
                _viewModel.CheckForCascade();
                return;
            }

            // Pause after row removal so player sees the gap
            await Task.Delay(200);

            // Highlight orphaned blocks before they start falling
            _boardDrawable.ShowFallingHighlight = true;
            BoardView.Invalidate();
            await Task.Delay(200);

            // Animate gravity: drop cells one row at a time
            while (_viewModel.ApplyGravityStep())
            {
                BoardView.Invalidate();
                await Task.Delay(150);
            }

            // Pause at landing so player sees final position
            await Task.Delay(200);

            _boardDrawable.ShowFallingHighlight = false;
            BoardView.Invalidate();

            // Pause before checking for new filled rows
            await Task.Delay(250);

            // Check if the fallen blocks formed new complete rows
            _viewModel.CheckForCascade();
        });
    }

    private void OnLinesCleared(int lines)
    {
        if (lines >= 4)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                StatusLabel.Text = "🔥 TETRIS! 🔥";
                StatusLabel.TextColor = Color.FromRgb(220, 50, 50);
                StatusLabel.FontSize = 42;
                StatusBorder.IsVisible = true;

                // Flash the board for emphasis
                for (int i = 0; i < 4; i++)
                {
                    BoardView.Opacity = 0.3;
                    await Task.Delay(100);
                    BoardView.Opacity = 1.0;
                    await Task.Delay(100);
                }

                await Task.Delay((int)_viewModel.Config.TetrisOverlayDurationMs - 800);

                if (StatusLabel.Text?.Contains("TETRIS") == true)
                {
                    StatusBorder.IsVisible = false;
                    StatusLabel.TextColor = Color.FromRgb(34, 34, 34);
                    StatusLabel.FontSize = 32;
                }
            });
        }
    }

    private void OnCascadeClear(int cascadeLevel, int linesCleared)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Brief pause to let player see gravity effect
            await Task.Delay(200);

            StatusLabel.Text = $"{linesCleared} row{(linesCleared > 1 ? "s" : "")} cleared by gravity falls";
            StatusLabel.TextColor = Color.FromRgb(200, 60, 200);
            StatusLabel.FontSize = 16;
            StatusBorder.IsVisible = true;
            StatusBorder.Opacity = 0;

            // Flash board to show cascade
            for (int i = 0; i < 2; i++)
            {
                BoardView.Opacity = 0.5;
                StatusBorder.Opacity = i % 2 == 0 ? 0.95 : 0.5;
                await Task.Delay(100);
                BoardView.Opacity = 1.0;
                await Task.Delay(100);
            }
            StatusBorder.Opacity = 0.95;

            await Task.Delay(700);

            if (StatusLabel.Text?.Contains("gravity falls") == true)
            {
                StatusBorder.IsVisible = false;
                StatusLabel.TextColor = Color.FromRgb(34, 34, 34);
                StatusLabel.FontSize = 32;
                StatusBorder.Opacity = 0.95;
            }
        });
    }

    private void OnLevelUp(int newLevel)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Wait for any Tetris message to finish first
            await Task.Delay(200);

            StatusLabel.Text = $"⚡ LEVEL {newLevel} ⚡\nSpeeding up!";
            StatusLabel.TextColor = Color.FromRgb(32, 96, 204);
            StatusLabel.FontSize = 28;
            StatusBorder.IsVisible = true;

            // Flash effect with board pulse
            for (int i = 0; i < 3; i++)
            {
                StatusBorder.Opacity = 0.4;
                BoardView.Opacity = 0.6;
                await Task.Delay(120);
                StatusBorder.Opacity = 0.95;
                BoardView.Opacity = 1.0;
                await Task.Delay(120);
            }

            await Task.Delay((int)_viewModel.Config.LevelUpFlashDurationMs - 720);

            if (StatusLabel.Text?.Contains("LEVEL") == true)
            {
                StatusBorder.IsVisible = false;
                StatusLabel.TextColor = Color.FromRgb(34, 34, 34);
                StatusLabel.FontSize = 32;
                StatusBorder.Opacity = 0.95;
                BoardView.Opacity = 1.0;
            }
        });
    }

    private void OnLevelUpRowsRemoved(int level, int rowsRemoved)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Wait for level-up message to finish
            await Task.Delay((int)_viewModel.Config.LevelUpFlashDurationMs + 400);

            // Highlight the bottom rows that will be removed
            _boardDrawable.HighlightBottomRows = rowsRemoved;
            BoardView.Invalidate();

            // Show highlight for 1.5s
            await Task.Delay(1500);

            // Actually remove the rows now
            _boardDrawable.HighlightBottomRows = 0;
            _viewModel.ExecuteLevelBonusRemoval();
            BoardView.Invalidate();

            // Show bonus message
            StatusLabel.Text = $"🧹 {rowsRemoved} row{(rowsRemoved > 1 ? "s" : "")} cleared!\nLevel bonus!";
            StatusLabel.TextColor = Color.FromRgb(180, 100, 0);
            StatusLabel.FontSize = 24;
            StatusBorder.IsVisible = true;
            StatusBorder.Opacity = 0;

            // Fade in
            for (int i = 1; i <= 4; i++)
            {
                StatusBorder.Opacity = i * 0.25;
                await Task.Delay(80);
            }

            // Keep on screen for 2.5 seconds
            await Task.Delay(2500);

            if (StatusLabel.Text?.Contains("cleared") == true)
            {
                StatusBorder.IsVisible = false;
                StatusLabel.TextColor = Color.FromRgb(34, 34, 34);
                StatusLabel.FontSize = 32;
                StatusBorder.Opacity = 0.95;
            }
        });
    }

    private bool _handlingGameOver;

    private void OnGameOver()
    {
        _gameActive = false;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_handlingGameOver) return;
            _handlingGameOver = true;

            ExitGameButton.IsVisible = false;

            int rank = -1;
            if (_viewModel.CheckHighScore())
            {
                rank = await PromptAndSaveHighScore();
            }

            ShowStatsOverlay(rank);
            _handlingGameOver = false;
        });
    }

    private void ShowStatsOverlay(int highlightRank = -1)
    {
        var elapsed = _viewModel.ElapsedTime;
        var modeText = _viewModel.Config.Mode == GameMode.Pro ? "Pro" : "Classic";

        StatsScoreLabel.Text = $"Score: {_viewModel.Score}";
        StatsLevelLabel.Text = $"Level: {_viewModel.Level}";
        StatsLinesLabel.Text = $"Total Lines: {_viewModel.TotalLines}";
        StatsTetrisLabel.Text = $"Tetrises: {_viewModel.TetrisCount}  |  Cascades: {_viewModel.CascadeCount} ({_viewModel.CascadeLines} lines)";
        StatsTimeLabel.Text = $"Time: {elapsed.Minutes}:{elapsed.Seconds:D2}";
        StatsModeLabel.Text = $"Mode: {modeText}";

        if (highlightRank > 0)
        {
            StatsRankLabel.Text = $"🏆 High Score! Ranked #{highlightRank}";
            StatsRankLabel.IsVisible = true;
        }
        else
        {
            StatsRankLabel.IsVisible = false;
        }

        PopulateHighScoresTable(highlightRank);

        StatsContinueButton.IsVisible = _viewModel.EndedVoluntarily;
        StatsPanel.IsVisible = true;
    }

    private void PopulateHighScoresTable(int highlightRank)
    {
        HighScoresStack.Children.Clear();
        var scores = _viewModel.HighScoreService.Scores;

        if (scores.Count == 0)
        {
            HighScoresStack.Children.Add(new Label
            {
                Text = "No high scores yet",
                FontSize = 12,
                TextColor = Color.FromRgb(136, 136, 136),
                HorizontalOptions = LayoutOptions.Center
            });
            return;
        }

        for (int i = 0; i < scores.Count; i++)
        {
            var entry = scores[i];
            bool isHighlighted = (i + 1) == highlightRank;

            var row = new Border
            {
                BackgroundColor = isHighlighted ? Color.FromRgba(255, 200, 50, 80) : Colors.Transparent,
                Stroke = isHighlighted ? Color.FromRgb(204, 150, 0) : Colors.Transparent,
                StrokeThickness = isHighlighted ? 1 : 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                Padding = new Thickness(8, 3)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(28)),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(70)),
                    new ColumnDefinition(new GridLength(50))
                }
            };

            var rankLabel = new Label
            {
                Text = $"#{i + 1}",
                FontSize = 12,
                FontAttributes = isHighlighted ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isHighlighted ? Color.FromRgb(180, 100, 0) : Color.FromRgb(100, 100, 100),
                VerticalOptions = LayoutOptions.Center
            };

            var nameLabel = new Label
            {
                Text = entry.Name,
                FontSize = 12,
                FontAttributes = isHighlighted ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isHighlighted ? Color.FromRgb(34, 34, 34) : Color.FromRgb(68, 68, 68),
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            var scoreLabel = new Label
            {
                Text = entry.Score.ToString("N0"),
                FontSize = 12,
                FontAttributes = isHighlighted ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isHighlighted ? Color.FromRgb(34, 34, 34) : Color.FromRgb(68, 68, 68),
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center
            };

            var levelLabel = new Label
            {
                Text = $"Lvl {entry.Level}",
                FontSize = 12,
                TextColor = Color.FromRgb(100, 100, 100),
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center
            };

            grid.Add(rankLabel, 0, 0);
            grid.Add(nameLabel, 1, 0);
            grid.Add(scoreLabel, 2, 0);
            grid.Add(levelLabel, 3, 0);

            row.Content = grid;
            HighScoresStack.Children.Add(row);
        }
    }

    private async Task<int> PromptAndSaveHighScore()
    {
        _dialogOpen = true;
        string name = await DisplayPromptAsync("High Score!",
            $"Score: {_viewModel.Score}\nEnter your name:",
            "Save", "Cancel", "Player", 20);
        _dialogOpen = false;

        if (!string.IsNullOrWhiteSpace(name))
        {
            return _viewModel.SaveHighScore(name);
        }
        return -1;
    }

    public bool HandleKeyDown(string key)
    {
        if (_dialogOpen) return false;

        // Stats overlay hotkeys
        if (StatsPanel.IsVisible)
        {
            switch (key.ToLowerInvariant())
            {
                case "c":
                    if (StatsContinueButton.IsVisible)
                        OnStatsContinueClicked(null, EventArgs.Empty);
                    return true;
                case "n":
                    OnPlayAgainClicked(null, EventArgs.Empty);
                    return true;
                case "q":
                    OnStatsExitClicked(null, EventArgs.Empty);
                    return true;
            }
            return false;
        }

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
                HandleSpacePress();
                return true;
            case "p":
            case "escape":
                _viewModel.TogglePause();
                return true;
            case "e":
                if (_gameActive) _viewModel.EndGame();
                return true;
            case "n":
            case "enter":
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

    private void HandleSpacePress()
    {
        var result = _viewModel.SpacePressed();

        switch (result)
        {
            case "invisible":
                // Double-tap detected — cancel any pending hold
                _holdDelayCts?.Cancel();
                _holdDelayCts = null;
                FlashStatus("👻 CLOAK", Color.FromRgba(160, 80, 220, 255));
                break;

            case "pending_hold":
                // Classic mode: wait to see if it's a double-tap
                _holdDelayCts?.Cancel();
                _holdDelayCts = new CancellationTokenSource();
                var cts = _holdDelayCts;
                _ = Task.Delay(310, cts.Token).ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _viewModel.ExecuteHold();
                            FlashHoldPanel();
                        });
                    }
                });
                break;

            case "none":
                // Pro mode single tap — do nothing, wait for potential double-tap
                break;
        }
    }

    private async void FlashHoldPanel()
    {
        HoldBorder.Stroke = Color.FromRgb(255, 200, 0);
        HoldBorder.StrokeThickness = 3;
        await Task.Delay(400);
        HoldBorder.Stroke = Color.FromRgb(68, 102, 170);
        HoldBorder.StrokeThickness = 1;
    }

    private async void FlashStatus(string text, Color color)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = color;
        StatusBorder.IsVisible = true;
        await Task.Delay(600);
        if (StatusLabel.Text == text)
        {
            StatusBorder.IsVisible = false;
        }
    }
}
