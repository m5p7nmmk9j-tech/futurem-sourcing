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
    public string Currency { get; set; } = "RMB";
    public string Status { get; set; } = "draft";
    public string PayStatus { get; set; } = "unpaid";
    public string? DeliveryTerms { get; set; }
    public string? PaymentTerms { get; set; }
    public long? ImporterProfileId { get; set; }
    public long? LabelTemplateId { get; set; }
    public long? MarkTemplateId { get; set; }
    public string ImporterSnapshotJson { get; set; } = "{}";
    public string LabelTemplateSnapshotJson { get; set; } = "{}";
    public string MarkTemplateSnapshotJson { get; set; } = "{}";
    public DateTime? ConfirmedAt { get; set; }
}
