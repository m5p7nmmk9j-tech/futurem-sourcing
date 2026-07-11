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

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonQty { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Cartons { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonLengthCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonWidthCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonHeightCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonCbm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCbm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonGwKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalGwKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonNwKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalNwKg { get; set; }

    public string? SupplierItemNo { get; set; }
    public string? CustomerItemNo { get; set; }
    public long? OrderProductId { get; set; }
    public long? SourceDocumentLineId { get; set; }
    public long? CustomerId { get; set; }
    public long? SupplierId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseUnitPriceSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalesUnitPriceSnapshot { get; set; }

    public long? WarehouseId { get; set; }
    public long? WarehouseLocationId { get; set; }
    public long? InventoryLotId { get; set; }
    public string? Remark { get; set; }
    public int SortNo { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}
