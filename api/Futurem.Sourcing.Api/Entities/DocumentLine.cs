using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class DocumentLine
{
    public long Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public long DocumentId { get; set; }
    public long? ProductId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Unit { get; set; } = "PCS";

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CartonQty { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Cartons { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CartonLengthCm { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CartonWidthCm { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CartonHeightCm { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal CartonCbm { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal TotalCbm { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CartonGwKg { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalGwKg { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CartonNwKg { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalNwKg { get; set; }

    public string? SupplierItemNo { get; set; }
    public string? CustomerItemNo { get; set; }
    public string? Remark { get; set; }
    public int SortNo { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}
