namespace Futurem.Sourcing.Api.Entities;

public class ApprovalStep : BaseEntity
{
    public long ApprovalRequestId { get; set; }
    public int StepNo { get; set; }
    public string StepName { get; set; } = string.Empty;
    public long? ApproverId { get; set; }
    public string Status { get; set; } = "pending";
    public string? Action { get; set; }
    public string? Comment { get; set; }
    public DateTime? ActionAt { get; set; }
}
