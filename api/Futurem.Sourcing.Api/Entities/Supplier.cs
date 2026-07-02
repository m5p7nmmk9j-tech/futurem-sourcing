namespace Futurem.Sourcing.Api.Entities;

public class Supplier : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? MarketId { get; set; }
    public string? ShopNo { get; set; }
    public string? FloorNo { get; set; }
    public string? BoothNo { get; set; }
    public string? MainProducts { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Wechat { get; set; }
    public string? Whatsapp { get; set; }
    public string? Email { get; set; }
}
