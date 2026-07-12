namespace Futurem.Sourcing.Api.Entities;

public class OrderProductImage : BaseEntity
{
    public long OrderProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageType { get; set; } = "detail";
    public int SortNo { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
}
