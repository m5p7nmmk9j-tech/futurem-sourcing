using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class AuditTrailServiceTests
{
    [Fact]
    public async Task WriteAsync_PersistsBeforeAfterReasonAndCorrelation()
    {
        await using var db = TestDbFactory.Create();
        var service = new AuditTrailService(db);

        await service.WriteAsync(
            "CustomerOrder",
            42,
            "confirm",
            new { status = "draft" },
            new { status = "confirmed" },
            "客户确认",
            7,
            "corr-001");

        var row = await db.AuditLogs.SingleAsync();
        Assert.Equal("CustomerOrder", row.TargetType);
        Assert.Equal(42, row.TargetId);
        Assert.Contains("draft", row.BeforeJson);
        Assert.Contains("confirmed", row.AfterJson);
        Assert.Equal("客户确认", row.Reason);
        Assert.Equal("corr-001", row.CorrelationId);
    }
}
