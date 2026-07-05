namespace Futurem.Sourcing.Api.Entities;

public class MigrationHistory : BaseEntity
{
    public string MigrationName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "success";
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
