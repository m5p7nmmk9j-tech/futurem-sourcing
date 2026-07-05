namespace Futurem.Sourcing.Api.Entities;

public class RolePermission : BaseEntity
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
}
