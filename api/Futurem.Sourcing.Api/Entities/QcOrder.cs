namespace Futurem.Sourcing.Api.Entities;

public class QcOrder : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long? PurchaseOrderId { get; set; }
    public long? ReceivingOrderId { get; set; }
    public DateTime? QcDate { get; set; }
    public string Status { get; set; } = "draft";
    public string Result { get; set; } = "pending";
    public int ConfirmationVersion { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public long? UnlockedBy { get; set; }
    public string? UnlockReason { get; set; }
}
