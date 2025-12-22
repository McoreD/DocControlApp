namespace DocControl.Core.Models;

public sealed class NlqResponse
{
    public string? ProjectName { get; init; }
    public string? DocumentType { get; init; }
    public string? Owner { get; init; }
    public string Level1 { get; init; } = string.Empty;
    public string Level2 { get; init; } = string.Empty;
    public string Level3 { get; init; } = string.Empty;
    public string? Level4 { get; init; }
    public string FreeText { get; init; } = string.Empty;
}
