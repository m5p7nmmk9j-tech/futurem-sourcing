using System.Security.Claims;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/financial-adjustments")]
public sealed class FinancialAdjustmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FinancialAdjustmentService _service;

    public FinancialAdjustmentsController(AppDbContext db, FinancialAdjustmentService service)
    {
        _db = db;
        _service = service;
    }

    public sealed record CreateRequest(
        long FinanceRecordId,
        string AdjustmentType,
        decimal Amount,
        string Reason,
        string SourceType,
        long? SourceId,
        long? FinanceRecordLineId,
        long? QcOrderId,
        long? QcOrderLineId,
        long? ShipmentId,
        long? ShipmentExpenseId);

    public sealed record CancelRequest(string Reason);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? adjustmentType,
        [FromQuery] long? financeRecordId)
    {
        var query = _db.FinancialAdjustments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(adjustmentType)) query = query.Where(x => x.AdjustmentType == adjustmentType);
        if (financeRecordId.HasValue) query = query.Where(x => x.FinanceRecordId == financeRecordId.Value);

        var adjustments = await query.OrderByDescending(x => x.Id).Take(500).ToListAsync();
        var financeIds = adjustments.Select(x => x.FinanceRecordId).Distinct().ToList();
        var records = await _db.FinanceRecords.Where(x => financeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return Ok(adjustments.Select(x =>
        {
            records.TryGetValue(x.FinanceRecordId, out var record);
            return new { adjustment = x, financeRecord = record };
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var adjustment = await _db.FinancialAdjustments.FindAsync(id);
        if (adjustment is null) return NotFound();
        var financeRecord = await _db.FinanceRecords.FindAsync(adjustment.FinanceRecordId);
        return Ok(new { adjustment, financeRecord });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRequest request)
        => Ok(await _service.CreateAsync(new FinancialAdjustmentCreateInput(
            request.FinanceRecordId,
            request.AdjustmentType,
            request.Amount,
            request.Reason,
            request.SourceType,
            request.SourceId,
            request.FinanceRecordLineId,
            request.QcOrderId,
            request.QcOrderLineId,
            request.ShipmentId,
            request.ShipmentExpenseId), CurrentUserId()));

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id)
        => Ok(await _service.ApproveAsync(id, CurrentUserId()));

    [HttpPost("{id:long}/apply")]
    public async Task<IActionResult> Apply(long id)
        => Ok(await _service.ApplyAsync(id, CurrentUserId()));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancelRequest request)
        => Ok(await _service.CancelAsync(id, request.Reason, CurrentUserId()));

    private long? CurrentUserId()
        => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
