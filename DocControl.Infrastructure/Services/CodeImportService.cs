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

        // Track current values at each level for hierarchical code building
        string currentLevel1 = "";
        string currentLevel2 = "";
        string currentLevel3 = "";
        string currentLevel4 = "";
        string currentLevel5 = "";

        foreach (var line in dataLines)
        {
            var parts = ParseCsvLine(line);
            if (parts.Length < 2) continue;

            if (!int.TryParse(parts[0].Trim(), out var level) || level < 1 || level > 6)
            {
                result.AddError($"Invalid level '{parts[0]}' in line: {line}");
                continue;
            }

            var code = parts[1].Trim();
            // Description is everything after the second comma (parts[2] onwards joined)
            var description = parts.Length > 2 ? string.Join(",", parts.Skip(2)).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(description)) description = string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                result.AddError($"Empty code in line: {line}");
                continue;
            }

            try
            {
                // Update the hierarchy based on level
                if (level == 1)
                {
                    currentLevel1 = code;
                    currentLevel2 = "";
                    currentLevel3 = "";
                    currentLevel4 = "";
                    currentLevel5 = "";
                }
                else if (level == 2)
                {
                    currentLevel2 = code;
                    currentLevel3 = "";
                    currentLevel4 = "";
                    currentLevel5 = "";
                }
                else if (level == 3)
                {
                    currentLevel3 = code;
                    currentLevel4 = "";
                    currentLevel5 = "";
                }
                else if (level == 4)
                {
                    currentLevel4 = code;
                    currentLevel5 = "";
                }
                else if (level == 5)
                {
                    currentLevel5 = code;
                }

                var key = CreateCodeSeriesKeyFromHierarchy(projectId, level, currentLevel1, currentLevel2, currentLevel3, currentLevel4, currentLevel5, code);
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

    private static CodeSeriesKey CreateCodeSeriesKeyFromHierarchy(
        long projectId,
        int level,
        string level1,
        string level2,
        string level3,
        string level4,
        string level5,
        string level6Code)
    {
        return level switch
        {
            1 => new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = "", Level3 = "", Level4 = null, Level5 = null, Level6 = null },
            2 => new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = level2, Level3 = "", Level4 = null, Level5 = null, Level6 = null },
            3 => new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = level2, Level3 = level3, Level4 = null, Level5 = null, Level6 = null },
            4 => new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = level2, Level3 = level3, Level4 = level4, Level5 = null, Level6 = null },
            5 => new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = level2, Level3 = level3, Level4 = level4, Level5 = level5, Level6 = null },
            6 => new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = level2, Level3 = level3, Level4 = level4, Level5 = level5, Level6 = level6Code },
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
