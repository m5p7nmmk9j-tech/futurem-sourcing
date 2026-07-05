namespace Futurem.Sourcing.Api.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string PermissionType { get; set; } = "page";
}
