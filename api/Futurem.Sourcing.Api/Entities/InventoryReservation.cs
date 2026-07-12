using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class InventoryReservation : BaseEntity
{
    public long ContainerLoadId { get; set; }
    public long InventoryLotId { get; set; }
    public long CustomerId { get; set; }
    public long WarehouseId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReservedCartons { get; set; }

    public string Status { get; set; } = "active";
    public DateTime LockedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
}
