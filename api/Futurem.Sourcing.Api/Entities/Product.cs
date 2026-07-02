namespace Futurem.Sourcing.Api.Entities;

public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string NameCn { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? NameEs { get; set; }
    public long? CategoryId { get; set; }
    public string? Brand { get; set; }
    public string Unit { get; set; } = "PCS";
    public string? CustomerItemNo { get; set; }
    public string? ImageUrl { get; set; }
}
