using System.Text.Json;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;

namespace Futurem.Sourcing.Api.Services;

public sealed class AuditTrailService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public AuditTrailService(AppDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        string entityType,
        long entityId,
        string action,
        object? before,
        object? after,
        string? reason,
        long? userId,
        string? correlationId = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Username = userId?.ToString() ?? "system",
            Action = action,
            Module = entityType,
            TargetType = entityType,
            TargetId = entityId,
            TargetNo = entityId.ToString(),
            IpAddress = string.Empty,
            UserAgent = string.Empty,
            BeforeJson = before is null ? string.Empty : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? string.Empty : JsonSerializer.Serialize(after, JsonOptions),
            Reason = reason,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            Result = "success",
            CreatedAt = DateTime.Now
        });

        await _db.SaveChangesAsync();
    }
}
