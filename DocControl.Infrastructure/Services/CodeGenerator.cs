using System.Text;
using DocControl.Core.Configuration;
using DocControl.Core.Models;

namespace DocControl.Infrastructure.Services;

public sealed class CodeGenerator
{
    private readonly DocumentConfig config;

    public CodeGenerator(DocumentConfig config)
    {
        this.config = config;
    }

    public string BuildCode(CodeSeriesKey key, int number)
    {
        var parts = new List<string> { key.Level1, key.Level2, key.Level3 };
        if (config.EnableLevel4 && !string.IsNullOrWhiteSpace(key.Level4))
        {
            parts.Add(key.Level4);
        }
        parts.Add(number.ToString().PadLeft(config.PaddingLength, '0'));
        return string.Join(config.Separator, parts);
    }

    public string BuildFileName(CodeSeriesKey key, int number, string freeText, string? extension)
    {
        var baseCode = BuildCode(key, number);
        var sb = new StringBuilder(baseCode);

        var trimmedFree = freeText?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedFree))
        {
            sb.Append(' ');
            sb.Append(trimmedFree);
        }

        if (!string.IsNullOrWhiteSpace(extension))
        {
            var sanitized = extension.StartsWith('.') ? extension : $".{extension}";
            sb.Append(sanitized);
        }
        return sb.ToString();
    }
}
