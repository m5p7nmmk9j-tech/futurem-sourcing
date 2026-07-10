using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class Payment : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public string Direction { get; set; } = "receive";
    public long FinanceRecordId { get; set; }
    public long? BankAccountId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public long TargetId { get; set; }
    public long? CustomerId { get; set; }
    public long? SupplierId { get; set; }
    public string PaymentMethod { get; set; } = "bank";
    public string Currency { get; set; } = "RMB";

    [Column(TypeName = "decimal(18,4)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1;

    [Column(TypeName = "decimal(18,4)")]
    public decimal FeeAmount { get; set; }

    public DateTime? PaymentDate { get; set; }
    public string? AttachmentUrl { get; set; }
}
