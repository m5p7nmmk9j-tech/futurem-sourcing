namespace Futurem.Sourcing.Api.Entities;

public class Role : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataScope { get; set; } = "all";
    public bool IsSystem { get; set; }
}
