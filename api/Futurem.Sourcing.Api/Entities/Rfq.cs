namespace Futurem.Sourcing.Api.Entities;

public class Rfq : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long? BuyingTripId { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? RequestDate { get; set; }
}
