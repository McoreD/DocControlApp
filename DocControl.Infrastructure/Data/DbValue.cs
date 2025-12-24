namespace DocControl.Infrastructure.Data;

internal static class DbValue
{
    public static string NormalizeLevel(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public static string? NormalizeRead(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
