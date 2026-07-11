using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class ShipmentExpense : BaseEntity
{
    public long ShipmentId { get; set; }
    public string ExpenseCode { get; set; } = "OTHER";
    public string ExpenseName { get; set; } = string.Empty;
    public string NormalizedExpenseName { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
    public long? SupplierId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RMB";
    public long? FinanceRecordId { get; set; }
    public string FinanceStatus { get; set; } = "pending";
    public int SortNo { get; set; }
}
