using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class SupplierPrepaymentUsage : BaseEntity
{
    public long SupplierPrepaymentId { get; set; }
    public long FinanceRecordId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public string UsageType { get; set; } = "apply";
}
