# Tetris — .NET MAUI

A classic Tetris game built with .NET MAUI, featuring:

- All 7 standard tetrominoes (I, O, T, S, Z, J, L) with SRS wall kicks
- 10×20 board with ghost piece preview
- Next 2 pieces preview panel
- Hold/reserve piece (one swap per piece drop)
- Scoring: soft drop, hard drop, line clears amplified by level
- Level progression (every 10 lines) with increasing speed
- Persistent local high scores (top 10)
- Fully configurable game parameters in `Config/GameConfig.cs`

## Controls

| Action | Key |
|--------|-----|
| Move Left | ← or A |
| Move Right | → or D |
| Rotate CW | ↑ or W |
| Rotate CCW | Z |
| Soft Drop | ↓ or S |
| Hard Drop | Space |
| Hold Piece | C or Shift |
| Pause | P or Escape |

## Building

```bash
dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0
```

## Configuration

All game settings are in [`Config/GameConfig.cs`](Config/GameConfig.cs) and can be adjusted without changing game logic:
- Board dimensions, preview count
- Speed/timing (base interval, multiplier per level)
- Scoring (drop points, line clear points)
- Piece colors
- Visual settings (cell size, ghost opacity)

## Architecture

```
Config/     — Configurable game parameters
Models/     — Tetromino, GameBoard, GameState, HighScore
Services/   — GameEngine, ScoreService, HighScoreService
Views/      — GamePage, GameBoardDrawable, PreviewDrawable
ViewModels/ — GameViewModel (MVVM binding)
```
