namespace Futurem.Sourcing.Api.Entities;

public class RefreshToken : BaseEntity
{
    public long UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string RevokedReason { get; set; } = string.Empty;
    public string CreatedByIp { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.Now;
}
