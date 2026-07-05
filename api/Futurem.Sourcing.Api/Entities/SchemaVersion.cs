namespace Futurem.Sourcing.Api.Entities;

public class SchemaVersion : BaseEntity
{
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "current";
    public DateTime AppliedAt { get; set; } = DateTime.Now;
    public string Notes { get; set; } = string.Empty;
}
