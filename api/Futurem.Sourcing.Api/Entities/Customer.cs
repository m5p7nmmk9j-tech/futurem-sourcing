using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class Customer : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Port { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Whatsapp { get; set; }
    public string? Email { get; set; }
    public string Currency { get; set; } = "RMB";
    [Column(TypeName = "decimal(18,4)")]
    public decimal CreditLimit { get; set; }
    public int CreditDays { get; set; }
}
