namespace Tetris.Models;

/// <summary>
/// Represents a single high score entry.
/// </summary>
public class HighScore
{
    public string Name { get; set; } = "Player";
    public int Score { get; set; }
    public int Level { get; set; }
    public int Lines { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}
