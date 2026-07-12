namespace Futurem.Sourcing.Api.Entities;

public class LogisticsProvider : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServiceTypesJson { get; set; } = "[]";
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public string? BankInfoJson { get; set; }
    public string Status { get; set; } = "active";
}
