namespace Futurem.Sourcing.Api.Entities;

public class PrintTemplate : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string PaperSize { get; set; } = "A4";
    public string Body { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string Status { get; set; } = "active";
}
