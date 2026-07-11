using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class SupplierPrepayment : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string Currency { get; set; } = "RMB";

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AvailableAmount { get; set; }

    public string SourceType { get; set; } = "SHIPMENT_EXPENSE_OVERPAYMENT";
    public long SourceId { get; set; }
    public long? SourceFinanceRecordId { get; set; }
    public string Status { get; set; } = "available";
}
