namespace Futurem.Sourcing.Api.Entities;

public class CustomerOrder : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long? BuyingTripId { get; set; }
    public long CustomerId { get; set; }
    public long? RfqId { get; set; }
    public DateTime? OrderDate { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "draft";
}
