namespace Tetris.Services;

using System.Text.Json;
using Tetris.Models;

/// <summary>
/// Manages persistent high score storage (top 10).
/// </summary>
public class HighScoreService
{
    private const int MaxScores = 10;
    private readonly string _filePath;
    private List<HighScore> _scores = [];

    public HighScoreService()
    {
        var appData = FileSystem.AppDataDirectory;
        _filePath = Path.Combine(appData, "highscores.json");
        Load();
    }

    public IReadOnlyList<HighScore> Scores => _scores.AsReadOnly();

    public bool IsHighScore(int score)
    {
        return _scores.Count < MaxScores || score > _scores[^1].Score;
    }

    /// <summary>
    /// Adds a score and returns the 1-based rank (or -1 if not placed).
    /// </summary>
    public int AddScore(HighScore entry)
    {
        _scores.Add(entry);
        _scores = [.. _scores.OrderByDescending(s => s.Score).Take(MaxScores)];
        Save();

        // Find the rank by matching the entry
        for (int i = 0; i < _scores.Count; i++)
        {
            if (_scores[i].Score == entry.Score && _scores[i].Name == entry.Name && _scores[i].Date == entry.Date)
                return i + 1;
        }
        return -1;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _scores = JsonSerializer.Deserialize<List<HighScore>>(json) ?? [];
            }
        }
        catch
        {
            _scores = [];
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_scores, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Silent fail on save - non-critical
        }
    }
}
