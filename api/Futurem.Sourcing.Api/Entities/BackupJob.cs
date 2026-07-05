namespace Futurem.Sourcing.Api.Entities;

public class BackupJob : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string ScheduleType { get; set; } = "manual";
    public string BackupScope { get; set; } = "database";
    public string StoragePath { get; set; } = "backups";
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string Status { get; set; } = "ready";
}
