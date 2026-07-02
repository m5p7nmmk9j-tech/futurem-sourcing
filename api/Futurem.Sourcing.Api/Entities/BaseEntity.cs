namespace Futurem.Sourcing.Api.Entities;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public bool IsDeleted { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public long? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Remark { get; set; }
}
