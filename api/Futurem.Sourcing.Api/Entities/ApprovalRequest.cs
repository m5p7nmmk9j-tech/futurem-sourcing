namespace Futurem.Sourcing.Api.Entities;

public class ApprovalRequest : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public string ApprovalType { get; set; } = "general";
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RMB";
    public string Status { get; set; } = "draft";
    public long? ApplicantId { get; set; }
    public long? CurrentApproverId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
