using System.Text.Json;
using App.Core.Models;

namespace WinFormsApp.Services;

internal sealed class AiAnalysisHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public AiAnalysisHistoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<AiAnalysisHistoryEntry> GetRecent(int count = 20)
    {
        var entries = LoadAll();
        return entries
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(Math.Max(1, count))
            .ToList();
    }

    public void Add(AiAnalysisHistoryEntry entry)
    {
        var entries = LoadAll()
            .Prepend(entry)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .ToList();

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(entries, JsonOptions));
    }

    private List<AiAnalysisHistoryEntry> LoadAll()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<AiAnalysisHistoryEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}
