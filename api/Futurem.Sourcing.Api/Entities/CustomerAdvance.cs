using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class CustomerAdvance : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public long? SourcePaymentId { get; set; }
    public long? SourceAdjustmentId { get; set; }
    public string Currency { get; set; } = "RMB";

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AvailableAmount { get; set; }

    public string Status { get; set; } = "available";
}
