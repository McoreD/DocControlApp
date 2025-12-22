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
        if (config.EnableLevel4)
        {
            if (parts.Length < 5) { reason = "Expected Level1-4 and number"; return false; }
            if (parts.Length > 6) { reason = "Too many parts"; return false; }
            var level1 = parts[0];
            var level2 = parts[1];
            var level3 = parts[2];
            var level4 = parts[3];
            if (!AllCodesValid(level1, level2, level3, level4)) { reason = "Codes must be alphanumeric (A-Z,0-9,_,-)"; return false; }
            if (!int.TryParse(parts[4], out var num)) { reason = "Number not numeric"; return false; }
            var freeText = parts.Length > 5 ? string.Join(config.Separator, parts.Skip(5)) : string.Empty;
            parsed = new ParsedFileName(new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = level2, Level3 = level3, Level4 = level4 }, num, freeText, ext);
            return true;
        }
        else
        {
            if (parts.Length < 4) { reason = "Expected Level1-3 and number"; return false; }
            if (parts.Length > 5) { reason = "Too many parts"; return false; }
            var level1 = parts[0];
            var level2 = parts[1];
            var level3 = parts[2];
            if (!AllCodesValid(level1, level2, level3)) { reason = "Codes must be alphanumeric (A-Z,0-9,_,-)"; return false; }
            if (!int.TryParse(parts[3], out var num)) { reason = "Number not numeric"; return false; }
            var freeText = parts.Length > 4 ? string.Join(config.Separator, parts.Skip(4)) : string.Empty;
            parsed = new ParsedFileName(new CodeSeriesKey { ProjectId = projectId, Level1 = level1, Level2 = level2, Level3 = level3 }, num, freeText, ext);
            return true;
        }
    }

    private static bool AllCodesValid(params string[] codes) => codes.All(c => CodeRegex.IsMatch(c));
}
