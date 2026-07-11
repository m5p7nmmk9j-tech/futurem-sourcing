namespace Futurem.Sourcing.Api.Entities;

public class AuditLog : BaseEntity
{
    public long? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public long? TargetId { get; set; }
    public string TargetNo { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string BeforeJson { get; set; } = string.Empty;
    public string AfterJson { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public string? SourceDocumentType { get; set; }
    public long? SourceDocumentId { get; set; }
    public string Result { get; set; } = "success";
}
