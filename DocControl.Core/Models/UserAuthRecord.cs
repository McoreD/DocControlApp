namespace DocControl.Core.Models;

public sealed class UserAuthRecord
{
    public long UserId { get; set; }
    public bool MfaEnabled { get; set; }
    public string? MfaMethodsJson { get; set; }
}
