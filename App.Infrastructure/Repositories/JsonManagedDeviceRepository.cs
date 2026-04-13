using System.Text.Json;
using App.Core.Interfaces;
using App.Core.Models;

namespace App.Infrastructure.Repositories;

public sealed class JsonManagedDeviceRepository : IManagedDeviceRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _syncRoot = new();

    public JsonManagedDeviceRepository(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<ManagedDevice> GetAll()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<ManagedDevice>();
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<ManagedDevice>();
            }

            return JsonSerializer.Deserialize<List<ManagedDevice>>(json, SerializerOptions) ??
                   new List<ManagedDevice>();
        }
    }

    public void SaveAll(IReadOnlyList<ManagedDevice> devices)
    {
        lock (_syncRoot)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(devices, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}
