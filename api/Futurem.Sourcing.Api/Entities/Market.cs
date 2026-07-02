namespace Futurem.Sourcing.Api.Entities;

public class Market : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Address { get; set; }
}
