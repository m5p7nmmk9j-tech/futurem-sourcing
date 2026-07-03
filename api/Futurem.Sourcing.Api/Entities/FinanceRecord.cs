using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class FinanceRecord : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public string RecordType { get; set; } = "receivable";
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public long? CustomerId { get; set; }
    public long? SupplierId { get; set; }
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,4)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal PaidAmount { get; set; }

    public DateTime? RecordDate { get; set; }
    public string Status { get; set; } = "pending";
}
