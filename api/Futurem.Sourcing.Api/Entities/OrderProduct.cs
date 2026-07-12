using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Futurem.Sourcing.Api.Entities;

public class OrderProduct : BaseEntity
{
    public long CustomerId { get; set; }
    public long SupplierId { get; set; }
    public long? SourceOrderProductId { get; set; }
    public long SourceCustomerOrderId { get; set; }

    [MaxLength(80)]
    public string SystemSku { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? CustomerItemNo { get; set; }

    [MaxLength(120)]
    public string CustomerBarcode { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? SupplierItemNo { get; set; }

    public string NameCn { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? NameEs { get; set; }
    public string? Specification { get; set; }
    public string? Color { get; set; }
    public string Unit { get; set; } = "PCS";

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseUnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalesUnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonQty { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonLengthCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonWidthCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonHeightCm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonCbm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonGwKg { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CartonNwKg { get; set; }

    public long ImporterProfileId { get; set; }
    public string ImporterSnapshotJson { get; set; } = "{}";
    public long LabelTemplateId { get; set; }
    public string LabelTemplateSnapshotJson { get; set; } = "{}";
    public long MarkTemplateId { get; set; }
    public string MarkTemplateSnapshotJson { get; set; } = "{}";

    [MaxLength(20)]
    public string BatchCode { get; set; } = string.Empty;

    public string Status { get; set; } = "draft";
    public DateTime? LockedAt { get; set; }
    public bool NeedsReview { get; set; }
}
