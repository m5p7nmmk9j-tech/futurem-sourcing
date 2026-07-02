namespace Futurem.Sourcing.Api.Entities;

public class ProductCategory : BaseEntity
{
    public long? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortNo { get; set; }
}
