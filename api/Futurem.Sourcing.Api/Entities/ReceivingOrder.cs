using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class ReceivingOrder : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long PurchaseOrderId { get; set; }
    public long? DeliveryNoticeId { get; set; }
    public long? WarehouseId { get; set; }
    public long? SupplierId { get; set; }
    public DateTime? ReceiveDate { get; set; }
    public string? WarehouseLocation { get; set; }
    public string Status { get; set; } = "draft";

    [Column(TypeName = "decimal(18,2)")]
    public decimal TemporaryQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TemporaryCartons { get; set; }
}
