namespace DocControl.Core.Models;

public sealed class ProjectMemberRecord
{
    public long ProjectId { get; set; }
    public long UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public long AddedByUserId { get; set; }
    public DateTime AddedAtUtc { get; set; }
}
