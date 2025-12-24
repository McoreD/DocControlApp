namespace DocControl.Core.Models;

public sealed class CodeCatalogRecord
{
    public long Id { get; set; }
    public CodeSeriesKey Key { get; set; } = null!;
    public string? Description { get; set; }
}
