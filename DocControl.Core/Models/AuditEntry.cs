namespace DocControl.Core.Models;

public sealed class AuditEntry
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? DocumentId { get; set; }
}
