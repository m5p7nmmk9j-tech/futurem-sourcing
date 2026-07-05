namespace Futurem.Sourcing.Api.Entities;

public class BackupHistory : BaseEntity
{
    public long? BackupJobId { get; set; }
    public string BackupNo { get; set; } = string.Empty;
    public string BackupType { get; set; } = "manual";
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = "success";
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
