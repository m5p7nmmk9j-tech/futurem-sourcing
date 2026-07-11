using System.Security.Claims;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/delivery-notices")]
public class DeliveryNoticesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DeliveryNoticeService _service;

    public DeliveryNoticesController(AppDbContext db, DeliveryNoticeService service)
    {
        _db = db;
        _service = service;
    }

    public sealed record GenerateRequest(long SummaryOrderId, DateTime PlannedDate, long WarehouseId);
    public sealed record CreateReceivingRequest(List<ReceivingLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? summaryOrderId,
        [FromQuery] long? supplierId,
        [FromQuery] string? status)
    {
        var query = _db.DeliveryNotices.AsQueryable();
        if (summaryOrderId.HasValue) query = query.Where(x => x.SummaryOrderId == summaryOrderId.Value);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var notices = await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
        var ids = notices.Select(x => x.Id).ToList();
        var lineCounts = await _db.DeliveryNoticeLines
            .Where(x => ids.Contains(x.DeliveryNoticeId))
            .GroupBy(x => x.DeliveryNoticeId)
            .Select(x => new { DeliveryNoticeId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.DeliveryNoticeId, x => x.Count);

        return Ok(notices.Select(notice => new
        {
            notice,
            lineCount = lineCounts.GetValueOrDefault(notice.Id)
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var notice = await _db.DeliveryNotices.FindAsync(id);
        if (notice is null) return NotFound();
        var lines = await _db.DeliveryNoticeLines
            .Where(x => x.DeliveryNoticeId == id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var productIds = lines.Select(x => x.OrderProductId).Distinct().ToList();
        var products = await _db.OrderProducts
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var poIds = lines.Select(x => x.PurchaseOrderId).Distinct().ToList();
        var purchaseOrders = await _db.PurchaseOrders
            .Where(x => poIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return Ok(new
        {
            notice,
            lines = lines.Select(line => new
            {
                line,
                product = products.GetValueOrDefault(line.OrderProductId),
                purchaseOrder = purchaseOrders.GetValueOrDefault(line.PurchaseOrderId)
            })
        });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(GenerateRequest request)
        => Ok(await _service.GenerateForConfirmedSummaryAsync(
            request.SummaryOrderId,
            request.PlannedDate,
            request.WarehouseId,
            CurrentUserId()));

    [HttpPost("{id:long}/publish")]
    public async Task<IActionResult> Publish(long id)
        => Ok(await _service.PublishAsync(id, CurrentUserId()));

    [HttpPost("{id:long}/supplier-confirm")]
    public async Task<IActionResult> SupplierConfirm(long id)
        => Ok(await _service.SupplierConfirmAsync(id, CurrentUserId()));

    [HttpPost("{id:long}/receivings")]
    public async Task<IActionResult> CreateReceiving(long id, CreateReceivingRequest request)
        => Ok(await _service.CreateReceivingAsync(id, request.Lines, CurrentUserId()));

    private long? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) ? id : null;
    }
}
