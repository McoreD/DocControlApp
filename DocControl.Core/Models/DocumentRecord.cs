namespace DocControl.Core.Models;

public sealed class DocumentRecord
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string Level1 { get; set; } = string.Empty;
    public string Level2 { get; set; } = string.Empty;
    public string Level3 { get; set; } = string.Empty;
    public string? Level4 { get; set; }
    public string? Level5 { get; set; }
    public string? Level6 { get; set; }
    public int Number { get; set; }
    public string? FreeText { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? OriginalQuery { get; set; }
    public long CodeSeriesId { get; set; }
}
