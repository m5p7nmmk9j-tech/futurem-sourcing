using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class DeliveryNoticeLine : BaseEntity
{
    public long DeliveryNoticeId { get; set; }
    public long SummaryOrderItemId { get; set; }
    public long PurchaseOrderId { get; set; }
    public long PurchaseOrderLineId { get; set; }
    public long OrderProductId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlannedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlannedCartons { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReceivedCartons { get; set; }
}
