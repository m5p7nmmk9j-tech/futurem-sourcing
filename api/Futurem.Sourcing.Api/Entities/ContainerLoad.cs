using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class ContainerLoad : BaseEntity
{
    public string No { get; set; } = string.Empty;
    public long? SummaryOrderId { get; set; }
    public string ContainerType { get; set; } = string.Empty;
    public string? ContainerNo { get; set; }
    public string? SealNo { get; set; }
    public DateTime? LoadDate { get; set; }
    public string Status { get; set; } = "draft";

    [Column(TypeName = "decimal(18,6)")]
    public decimal TotalCbm { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalGwKg { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalCartons { get; set; }
}
