using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class InventoryTransaction : BaseEntity
{
    public long InventoryLotId { get; set; }
    public long WarehouseId { get; set; }
    public long? WarehouseLocationId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public long? SourceId { get; set; }
    public string Reason { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantityDelta { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonsDelta { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal QuantityBalance { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonsBalance { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal LockedQuantityBalance { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal LockedCartonsBalance { get; set; }
}
