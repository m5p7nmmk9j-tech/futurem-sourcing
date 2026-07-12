using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class CustomerAdvanceUsage : BaseEntity
{
    public long CustomerAdvanceId { get; set; }
    public long FinanceRecordId { get; set; }
    public long? FinanceRecordLineId { get; set; }
    public string UsageType { get; set; } = "apply";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
}
