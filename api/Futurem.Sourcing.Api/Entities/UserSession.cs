namespace Futurem.Sourcing.Api.Entities;

public class UserSession : BaseEntity
{
    public long UserId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = "online";
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime LoginAt { get; set; } = DateTime.Now;
    public DateTime LastSeenAt { get; set; } = DateTime.Now;
    public DateTime? LogoutAt { get; set; }
}
