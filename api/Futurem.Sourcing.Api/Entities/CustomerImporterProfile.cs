namespace Futurem.Sourcing.Api.Entities;

public class CustomerImporterProfile : BaseEntity
{
    public long CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? TaxIdOrRfc { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public string DefaultOriginText { get; set; } = "Made in China";
    public long? DefaultLabelTemplateId { get; set; }
    public long? DefaultMarkTemplateId { get; set; }
    public bool IsDefault { get; set; }
    public string Status { get; set; } = "active";
}
