namespace DocControl.Core.Models;

/// <summary>
/// Identifies a series within a project.
/// </summary>
public sealed class CodeSeriesKey
{
    public long ProjectId { get; init; }
    public required string Level1 { get; init; }
    public required string Level2 { get; init; }
    public required string Level3 { get; init; }
    public string? Level4 { get; init; }
}
