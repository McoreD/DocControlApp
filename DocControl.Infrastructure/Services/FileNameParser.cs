using System.Text.RegularExpressions;
using DocControl.Core.Configuration;
using DocControl.Core.Models;

namespace DocControl.Infrastructure.Services;

public sealed class FileNameParser
{
    private readonly DocumentConfig config;
    private static readonly Regex CodeRegex = new("^[A-Za-z0-9_-]+$");

    public FileNameParser(DocumentConfig config)
    {
        this.config = config;
    }

    public bool TryParse(string fileName, out ParsedFileName? parsed)
    {
        return TryParse(fileName, out parsed, out _);
    }

    public bool TryParse(string fileName, out ParsedFileName? parsed, out string reason, long projectId = 0)
    {
        parsed = null;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName)) { reason = "Empty name"; return false; }

        var name = fileName;
        var ext = string.Empty;
        var lastDot = fileName.LastIndexOf('.');
        if (lastDot > 0)
        {
            name = fileName[..lastDot];
            ext = fileName[(lastDot + 1)..];
        }

        var parts = name.Split(config.Separator, StringSplitOptions.RemoveEmptyEntries);
        var levelCount = Math.Clamp(config.LevelCount, 1, 6);
        if (parts.Length < levelCount + 1)
        {
            reason = $"Expected {levelCount} level(s) and number";
            return false;
        }

        var levels = parts.Take(levelCount).ToArray();
        if (!AllCodesValid(levels))
        {
            reason = "Codes must be alphanumeric (A-Z,0-9,_,-)";
            return false;
        }

        if (!int.TryParse(parts[levelCount], out var num))
        {
            reason = "Number not numeric";
            return false;
        }

        var freeText = parts.Length > levelCount + 1
            ? string.Join(config.Separator, parts.Skip(levelCount + 1))
            : string.Empty;

        parsed = new ParsedFileName(new CodeSeriesKey
        {
            ProjectId = projectId,
            Level1 = levels.ElementAtOrDefault(0) ?? string.Empty,
            Level2 = levels.ElementAtOrDefault(1) ?? string.Empty,
            Level3 = levels.ElementAtOrDefault(2) ?? string.Empty,
            Level4 = levels.ElementAtOrDefault(3),
            Level5 = levels.ElementAtOrDefault(4),
            Level6 = levels.ElementAtOrDefault(5)
        }, num, freeText, ext);
        return true;
    }

    private static bool AllCodesValid(params string[] codes) => codes.All(c => CodeRegex.IsMatch(c));
}
