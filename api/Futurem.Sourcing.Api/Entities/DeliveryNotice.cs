using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class DeliveryNotice : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public long SummaryOrderId { get; set; }
    public long SupplierId { get; set; }
    public long WarehouseId { get; set; }
    public DateTime PlannedDeliveryDate { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? PublishedAt { get; set; }
    public DateTime? SupplierConfirmedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCartons { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReceivedCartons { get; set; }
}
