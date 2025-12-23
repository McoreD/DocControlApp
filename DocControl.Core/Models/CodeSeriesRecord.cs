namespace DocControl.Core.Models;

/// <summary>
/// Represents a saved code series with its metadata.
/// </summary>
public sealed class CodeSeriesRecord
{
    public long Id { get; init; }
    public required CodeSeriesKey Key { get; init; }
    public string? Description { get; init; }
    public int NextNumber { get; init; }
}
