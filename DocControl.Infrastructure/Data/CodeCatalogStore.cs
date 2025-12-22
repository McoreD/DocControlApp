using System.Text.Json;
using System.Threading;

namespace DocControl.Infrastructure.Data;

public sealed record CodeCatalogEntry(int Level, string Code, string? Description);

public sealed class CodeCatalogStore
{
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public CodeCatalogStore(string filePath)
    {
        this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public async Task<IReadOnlyList<CodeCatalogEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UpsertAsync(IEnumerable<CodeCatalogEntry> entries, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = (await LoadInternalAsync(cancellationToken).ConfigureAwait(false)).ToList();
            foreach (var entry in entries)
            {
                var existing = list.FirstOrDefault(e => e.Level == entry.Level && string.Equals(e.Code, entry.Code, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    list.Add(entry);
                }
                else
                {
                    list.Remove(existing);
                    list.Add(entry);
                }
            }
            await SaveInternalAsync(list, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteAsync(int level, string code, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = (await LoadInternalAsync(cancellationToken).ConfigureAwait(false)).ToList();
            list.RemoveAll(e => e.Level == level && string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));
            await SaveInternalAsync(list, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(new List<CodeCatalogEntry>(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ReplaceAsync(IEnumerable<CodeCatalogEntry> entries, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(entries.ToList(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<CodeCatalogEntry>> LoadInternalAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(filePath))
        {
            return Array.Empty<CodeCatalogEntry>();
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<CodeCatalogEntry>();
        return JsonSerializer.Deserialize<List<CodeCatalogEntry>>(json) ?? new List<CodeCatalogEntry>();
    }

    private async Task SaveInternalAsync(IReadOnlyList<CodeCatalogEntry> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }
}
