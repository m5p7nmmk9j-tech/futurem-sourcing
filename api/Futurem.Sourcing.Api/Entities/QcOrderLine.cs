using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class QcOrderLine : BaseEntity
{
    public long QcOrderId { get; set; }
    public long ReceivingOrderId { get; set; }
    public long ReceivingLineId { get; set; }
    public long? DeliveryNoticeLineId { get; set; }
    public long PurchaseOrderId { get; set; }
    public long? PurchaseOrderLineId { get; set; }
    public long OrderProductId { get; set; }
    public long SupplierId { get; set; }
    public long? WarehouseId { get; set; }
    public int ConfirmationVersion { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ArrivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal QualifiedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnqualifiedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReturnedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PendingQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AcceptedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseUnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PayableAmount { get; set; }
}
