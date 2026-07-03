namespace Futurem.Sourcing.Api.Entities;

public class ReceivingOrder : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long PurchaseOrderId { get; set; }
    public DateTime? ReceiveDate { get; set; }
    public string? WarehouseLocation { get; set; }
    public string Status { get; set; } = "draft";
}
