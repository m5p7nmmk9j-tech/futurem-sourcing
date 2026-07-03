namespace Futurem.Sourcing.Api.Entities;

public class PurchaseOrder : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long? BuyingTripId { get; set; }
    public long? CustomerOrderId { get; set; }
    public long SupplierId { get; set; }
    public long? CustomerId { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Currency { get; set; } = "CNY";
    public string Status { get; set; } = "draft";
    public string PayStatus { get; set; } = "unpaid";
}
