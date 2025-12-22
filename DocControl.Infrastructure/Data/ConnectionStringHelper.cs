using System.Web;
using Npgsql;

namespace DocControl.Infrastructure.Data;

public static class ConnectionStringHelper
{
    private static readonly HashSet<string> UnsupportedParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "channel_binding"
    };

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new ArgumentException("Connection string is required", nameof(raw));

        // Convert postgresql:// URIs and drop unsupported params
        if (raw.TrimStart().StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            raw = StripUnsupportedFromUri(raw);
        }
        else
        {
            raw = StripUnsupportedFromKv(raw);
        }

        var builder = new NpgsqlConnectionStringBuilder(raw)
        {
            SslMode = SslMode.Require
        };

        return builder.ToString();
    }

    private static string StripUnsupportedFromUri(string uriString)
    {
        var uri = new Uri(uriString);
        var query = HttpUtility.ParseQueryString(uri.Query);
        foreach (var key in UnsupportedParams)
        {
            query.Remove(key);
        }

        var builder = new UriBuilder(uri)
        {
            Query = query.ToString()
        };
        return builder.Uri.ToString();
    }

    private static string StripUnsupportedFromKv(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (UnsupportedParams.Contains(kv[0].Trim())) continue;
            kept.Add(part);
        }
        return string.Join(';', kept);
    }
}
