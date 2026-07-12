using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class FinancialAdjustment : BaseEntity
{
    public long FinanceRecordId { get; set; }
    public long? FinanceRecordLineId { get; set; }
    public long? QcOrderId { get; set; }
    public long? QcOrderLineId { get; set; }
    public long? ShipmentId { get; set; }
    public long? ShipmentExpenseId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public long? SourceId { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public DateTime AdjustmentDate { get; set; } = DateTime.Today;

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ResultAmount { get; set; }

    public string Reason { get; set; } = string.Empty;
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long? AppliedBy { get; set; }
    public DateTime? AppliedAt { get; set; }
    public long? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
}
