namespace Futurem.Sourcing.Api.Entities;

public class WarehouseLocation : BaseEntity
{
    public long WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Bin { get; set; }
    public string Status { get; set; } = "active";
}
