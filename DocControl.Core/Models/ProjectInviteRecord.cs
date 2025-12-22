namespace DocControl.Core.Models;

public sealed class ProjectInviteRecord
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string InviteTokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
}
