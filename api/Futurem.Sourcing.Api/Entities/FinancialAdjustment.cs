using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class FinancialAdjustment : BaseEntity
{
    public long FinanceRecordId { get; set; }
    public long QcOrderId { get; set; }
    public long QcOrderLineId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime AdjustmentDate { get; set; } = DateTime.Today;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
}
