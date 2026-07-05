namespace Futurem.Sourcing.Api.Entities;

public class LoginLog : BaseEntity
{
    public long? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Result { get; set; } = "success";
    public string Message { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime LoginAt { get; set; } = DateTime.Now;
}
