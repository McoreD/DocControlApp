using System.Text.Json;

namespace DocControl.Core.Security;

/// <summary>
/// Persists API keys in a local JSON file. Keys are stored as plain text; ensure filesystem access is restricted if needed.
/// </summary>
public sealed class JsonFileApiKeyStore : IApiKeyStore
{
    private readonly string filePath;
    private readonly object gate = new();

    public JsonFileApiKeyStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path is required", nameof(filePath));
        this.filePath = filePath;
    }

    public Task SaveAsync(string name, string apiKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (gate)
        {
            var dict = LoadInternal();
            dict[name] = apiKey;
            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, json);
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (gate)
        {
            var dict = LoadInternal();
            return Task.FromResult(dict.TryGetValue(name, out var value) ? value : null);
        }
    }

    private Dictionary<string, string> LoadInternal()
    {
        if (!File.Exists(filePath)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return dict ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
