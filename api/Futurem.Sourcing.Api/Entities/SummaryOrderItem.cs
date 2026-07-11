using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class SummaryOrderItem : BaseEntity
{
    public long SummaryOrderId { get; set; }
    public long PurchaseOrderId { get; set; }
    public long PurchaseOrderLineId { get; set; }
    public long OrderProductId { get; set; }
    public long SupplierId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservedCartons { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservedQuantity { get; set; }

    public string ReservationStatus { get; set; } = "draft_reserved";
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
}
