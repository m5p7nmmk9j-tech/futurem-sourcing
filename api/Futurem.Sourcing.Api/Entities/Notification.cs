namespace Futurem.Sourcing.Api.Entities;

public class Notification : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
    public string SourceType { get; set; } = "system";
    public long? SourceId { get; set; }
    public string Status { get; set; } = "unread";
    public DateTime? ReadAt { get; set; }
}
