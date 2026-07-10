using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class BankAccount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? AccountNo { get; set; }
    public string Currency { get; set; } = "RMB";

    [Column(TypeName = "decimal(18,4)")]
    public decimal OpeningBalance { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentBalance { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
