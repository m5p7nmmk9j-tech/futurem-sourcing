using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class ContainerLoadSource : BaseEntity
{
    public long ContainerLoadId { get; set; }
    public long InventoryReservationId { get; set; }
    public long InventoryLotId { get; set; }
    public long CustomerId { get; set; }
    public long WarehouseId { get; set; }
    public long OrderProductId { get; set; }
    public long PurchaseOrderId { get; set; }
    public long? PurchaseOrderLineId { get; set; }
    public long? SummaryOrderId { get; set; }
    public long ReceivingOrderId { get; set; }
    public long QcOrderId { get; set; }
    public long SupplierId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlannedQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal PlannedCartons { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualCartons { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseUnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal SalesUnitPrice { get; set; }
    [Column(TypeName = "decimal(18,6)")]
    public decimal ActualCbm { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal ActualGrossWeightKg { get; set; }
    public string Status { get; set; } = "loaded";
}
