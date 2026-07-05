namespace Futurem.Sourcing.Api.Entities;

public class UserAccount : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public long? RoleId { get; set; }
    public long? CompanyId { get; set; }
    public long? StoreId { get; set; }
    public long? WarehouseId { get; set; }
    public string Status { get; set; } = "active";
}
