namespace Futurem.Sourcing.Api.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Product : BaseEntity
{
    [MaxLength(80)]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Barcode { get; set; } = string.Empty;
    public string NameCn { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? NameEs { get; set; }
    public long? CategoryId { get; set; }
    public string? Brand { get; set; }
    public string Unit { get; set; } = "PCS";
    public string? CustomerItemNo { get; set; }
    public string? ImageUrl { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonQty { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonLengthCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonWidthCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonHeightCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonGwKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonNwKg { get; set; }
}
