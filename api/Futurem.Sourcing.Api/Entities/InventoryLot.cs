using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class InventoryLot : BaseEntity
{
    public string LotNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public long OrderProductId { get; set; }
    public long PurchaseOrderId { get; set; }
    public long? PurchaseOrderLineId { get; set; }
    public long? SummaryOrderId { get; set; }
    public long? DeliveryNoticeId { get; set; }
    public long? DeliveryNoticeLineId { get; set; }
    public long ReceivingOrderId { get; set; }
    public long ReceivingLineId { get; set; }
    public long QcOrderId { get; set; }
    public long QcOrderLineId { get; set; }
    public long SupplierId { get; set; }
    public long WarehouseId { get; set; }
    public long? WarehouseLocationId { get; set; }
    public string Status { get; set; } = "available";

    [Column(TypeName = "decimal(18,2)")]
    public decimal OnHandQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal LockedQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal OnHandCartons { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal LockedCartons { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonQty { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonCbm { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonGwKg { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonNwKg { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseUnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal SalesUnitPrice { get; set; }

    [NotMapped]
    public decimal AvailableQuantity => Math.Round(Math.Max(0m, OnHandQuantity - LockedQuantity), 2, MidpointRounding.AwayFromZero);
    [NotMapped]
    public decimal AvailableCartons => Math.Round(Math.Max(0m, OnHandCartons - LockedCartons), 2, MidpointRounding.AwayFromZero);
}
