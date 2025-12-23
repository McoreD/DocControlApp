using DocControl.Infrastructure.Data;
using DocControl.Core.Models;

namespace DocControl.Infrastructure.Services;

public sealed class CodeImportService
{
    private readonly CodeSeriesRepository codeSeriesRepository;

    public CodeImportService(CodeSeriesRepository codeSeriesRepository)
    {
        this.codeSeriesRepository = codeSeriesRepository;
    }

    public async Task<CodeImportResult> ImportCodesFromCsvAsync(long projectId, string csvContent, CancellationToken cancellationToken = default)
    {
        var result = new CodeImportResult();
        // Split on CR/LF using explicit char array to avoid overload ambiguity on newer frameworks
        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length == 0)
        {
            result.AddError("CSV file is empty");
            return result;
        }

        // Skip header row if it exists
        var dataLines = lines.Skip(1);
        var codeSeriesData = new List<(CodeSeriesKey key, string description, int maxNumber)>();

        foreach (var line in dataLines)
        {
            var parts = ParseCsvLine(line);
            if (parts.Length < 2) continue;

            if (!int.TryParse(parts[0].Trim(), out var level) || level < 1 || level > 4)
            {
                result.AddError($"Invalid level '{parts[0]}' in line: {line}");
                continue;
            }

            var code = parts[1].Trim();
            // Description is everything after the second comma (parts[2] onwards joined)
            var description = parts.Length > 2 ? string.Join(",", parts.Skip(2)).Trim() : "";

            if (string.IsNullOrWhiteSpace(code))
            {
                result.AddError($"Empty code in line: {line}");
                continue;
            }

            try
            {
                var key = CreateCodeSeriesKey(projectId, level, code);
                codeSeriesData.Add((key, description, 1)); // Start with NextNumber = 1
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.AddError($"Failed to process code '{code}': {ex.Message}");
            }
        }

        if (codeSeriesData.Count > 0)
        {
            try
            {
                await codeSeriesRepository.SeedNextNumbersAsync(codeSeriesData, cancellationToken);
            }
            catch (Exception ex)
            {
                result.AddError($"Failed to save codes to database: {ex.Message}");
            }
        }

        return result;
    }

    private static CodeSeriesKey CreateCodeSeriesKey(long projectId, int level, string code)
    {
        return level switch
        {
            1 => new CodeSeriesKey { ProjectId = projectId, Level1 = code, Level2 = "", Level3 = "", Level4 = null },
            2 => new CodeSeriesKey { ProjectId = projectId, Level1 = "", Level2 = code, Level3 = "", Level4 = null },
            3 => new CodeSeriesKey { ProjectId = projectId, Level1 = "", Level2 = "", Level3 = code, Level4 = null },
            4 => new CodeSeriesKey { ProjectId = projectId, Level1 = "", Level2 = "", Level3 = "", Level4 = code },
            _ => throw new ArgumentException($"Invalid level: {level}")
        };
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        result.Add(current.ToString());
        return result.ToArray();
    }
}

public sealed class CodeImportResult
{
    private readonly List<string> errors = [];
    
    public int SuccessCount { get; set; }
    public IReadOnlyList<string> Errors => errors;
    public bool HasErrors => errors.Count > 0;
    
    public void AddError(string error) => errors.Add(error);
}
