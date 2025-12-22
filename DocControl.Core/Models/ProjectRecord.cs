namespace DocControl.Core.Models;

public sealed class ProjectRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsArchived { get; set; }
}
