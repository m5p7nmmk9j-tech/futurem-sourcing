using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class FinanceRecordLine : BaseEntity
{
    public long FinanceRecordId { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public string LineType { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public long? SourceId { get; set; }
    public long? OrderProductId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    public string? Description { get; set; }
    public string Status { get; set; } = "pending";
}
