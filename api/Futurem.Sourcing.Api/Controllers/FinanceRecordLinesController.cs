using Futurem.Sourcing.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/finance-records/{financeRecordId:long}/lines")]
public sealed class FinanceRecordLinesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FinanceRecordLinesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(long financeRecordId)
    {
        var record = await _db.FinanceRecords.FindAsync(financeRecordId);
        if (record is null) return NotFound();
        var lines = await _db.FinanceRecordLines
            .Where(x => x.FinanceRecordId == financeRecordId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();
        return Ok(new
        {
            record,
            lines,
            outstandingAmount = Services.FinanceBalanceService.Outstanding(record)
        });
    }
}
