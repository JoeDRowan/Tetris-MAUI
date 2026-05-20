namespace Tetris.Views;

using Tetris.Config;
using Tetris.ViewModels;

public partial class GamePage : ContentPage
{
    private readonly GameViewModel _viewModel;
    private readonly GameBoardDrawable _boardDrawable;
    private readonly PreviewDrawable _nextDrawable;
    private readonly PreviewDrawable _holdDrawable;

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

        // Size the board view
        BoardView.WidthRequest = _viewModel.Config.Columns * _viewModel.Config.CellSize + 4;
        BoardView.HeightRequest = _viewModel.Config.Rows * _viewModel.Config.CellSize + 4;

        _viewModel.Redraw += OnRedraw;
        _viewModel.OnGameOver += OnGameOver;
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

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    private void OnNewGameClicked(object? sender, EventArgs e)
    {
        _viewModel.StartNewGame(Dispatcher);
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

    private async void OnGameOver()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_viewModel.CheckHighScore())
            {
                string name = await DisplayPromptAsync("High Score!", 
                    $"Score: {_viewModel.Score}\nEnter your name:", 
                    "Save", "Cancel", "Player", 20);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    _viewModel.SaveHighScore(name);
                }
            }
            else
            {
                await DisplayAlert("Game Over", $"Score: {_viewModel.Score}\nLevel: {_viewModel.Level}\nLines: {_viewModel.Lines}", "OK");
            }
        });
    }

    /// <summary>
    /// Handle keyboard input. Called from platform-specific key handler.
    /// </summary>
    public bool HandleKeyDown(string key)
    {
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
            case "z":
                _viewModel.RotateCounterClockwise();
                return true;
            case "down":
            case "s":
                _viewModel.StartSoftDrop();
                return true;
            case "space":
                _viewModel.HardDrop();
                return true;
            case "c":
            case "shift":
                _viewModel.HoldPiece();
                return true;
            case "p":
            case "escape":
                _viewModel.TogglePause();
                return true;
        }
        return false;
    }

    public bool HandleKeyUp(string key)
    {
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
