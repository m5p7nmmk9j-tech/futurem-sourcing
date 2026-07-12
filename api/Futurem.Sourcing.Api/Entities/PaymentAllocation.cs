using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class PaymentAllocation : BaseEntity
{
    public long PaymentId { get; set; }
    public long FinanceRecordId { get; set; }
    public long? FinanceRecordLineId { get; set; }
    public int AllocationOrder { get; set; }
    public string AllocationType { get; set; } = "apply";
    public long? ReversedAllocationId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
}
