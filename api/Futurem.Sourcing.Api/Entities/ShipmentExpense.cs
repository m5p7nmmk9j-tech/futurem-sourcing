using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class ShipmentExpense : BaseEntity
{
    public long ShipmentId { get; set; }
    public string ExpenseCode { get; set; } = "OTHER";
    public string ExpenseName { get; set; } = string.Empty;
    public string NormalizedExpenseName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = "other_service";
    public bool IsCustom { get; set; }

    // Legacy product-supplier field retained for old data only.
    public long? SupplierId { get; set; }
    public long? LogisticsProviderId { get; set; }

    // Legacy amount mirrors ProviderCost during transition.
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProviderCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CustomerCharge { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProfitAmount { get; set; }

    public string Currency { get; set; } = "RMB";
    public bool NeedsCustomerChargeReview { get; set; }
    public long? FinanceRecordId { get; set; }
    public string FinanceStatus { get; set; } = "pending";
    public int SortNo { get; set; }
}
