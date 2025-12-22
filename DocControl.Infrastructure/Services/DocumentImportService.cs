using DocControl.Core.Models;

namespace DocControl.Infrastructure.Services;

public sealed class DocumentImportService
{
    public IReadOnlyList<DocumentImportEntry> ParseCodeAndFileLines(IEnumerable<string> lines)
    {
        var result = new List<DocumentImportEntry>();

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var line = raw.Trim();
            if (line.StartsWith('#')) continue;

            var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) continue;

            var code = parts[0].Trim();
            var fileName = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(code)) continue;
            result.Add(new DocumentImportEntry(code, fileName));
        }

        return result;
    }
}
