namespace Futurem.Sourcing.Api.Entities;

public class RestoreHistory : BaseEntity
{
    public long? BackupHistoryId { get; set; }
    public string RestoreNo { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = "success";
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool VerifiedBeforeRestore { get; set; }
}
