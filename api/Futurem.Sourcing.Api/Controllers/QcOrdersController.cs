using System.Security.Claims;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/qc-orders")]
public class QcOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly QcConfirmationService _service;

    public QcOrdersController(AppDbContext db, QcConfirmationService service)
    {
        _db = db;
        _service = service;
    }

    public sealed record CreateRequest(long ReceivingOrderId);
    public sealed record ConfirmRequest(List<QcLineResult> Lines);
    public sealed record UnlockRequest(string Reason);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<QcOrder>>> List(
        [FromQuery] long? purchaseOrderId,
        [FromQuery] long? receivingOrderId,
        [FromQuery] string? status)
    {
        var query = _db.QcOrders.AsQueryable();
        if (purchaseOrderId.HasValue) query = query.Where(x => x.PurchaseOrderId == purchaseOrderId.Value);
        if (receivingOrderId.HasValue) query = query.Where(x => x.ReceivingOrderId == receivingOrderId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var qc = await _db.QcOrders.FindAsync(id);
        if (qc is null) return NotFound();
        var qcLines = await _db.QcOrderLines
            .Where(x => x.QcOrderId == id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var receivingLines = qc.ReceivingOrderId.HasValue
            ? await _db.DocumentLines
                .Where(x => x.DocumentType == "RCV" && x.DocumentId == qc.ReceivingOrderId.Value && !x.IsDeleted)
                .OrderBy(x => x.SortNo)
                .ThenBy(x => x.Id)
                .ToListAsync()
            : [];
        var orderProductIds = receivingLines
            .Where(x => x.OrderProductId.HasValue)
            .Select(x => x.OrderProductId!.Value)
            .Distinct()
            .ToList();
        var products = await _db.OrderProducts
            .Where(x => orderProductIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var qcByReceivingLine = qcLines.ToDictionary(x => x.ReceivingLineId);

        return Ok(new
        {
            qcOrder = qc,
            lines = receivingLines.Select(receivingLine => new
            {
                receivingLine,
                qcLine = qcByReceivingLine.GetValueOrDefault(receivingLine.Id),
                product = receivingLine.OrderProductId.HasValue
                    ? products.GetValueOrDefault(receivingLine.OrderProductId.Value)
                    : null
            })
        });
    }

    [HttpPost]
    public async Task<ActionResult<QcOrder>> Create(CreateRequest request)
        => await _service.CreateDraftAsync(request.ReceivingOrderId, CurrentUserId());

    [HttpPost("{id:long}/confirm")]
    public async Task<ActionResult<QcOrder>> Confirm(long id, ConfirmRequest request)
        => await _service.ConfirmAsync(id, request.Lines, CurrentUserId());

    [HttpPost("{id:long}/unlock")]
    public async Task<ActionResult<QcOrder>> Unlock(long id, UnlockRequest request)
        => await _service.UnlockAsync(id, request.Reason, CurrentUserId());

    [HttpPost("{id:long}/copy")]
    public IActionResult Copy(long id)
        => StatusCode(StatusCodes.Status410Gone, new
        {
            code = "QC_COPY_REMOVED",
            message = "一张收货单只能对应一张验货单；需要修改时请解锁原验货单。"
        });

    [HttpPut("{id:long}")]
    public async Task<ActionResult<QcOrder>> Update(long id, QcOrder input)
    {
        var entity = await _db.QcOrders.FindAsync(id);
        if (entity == null) return NotFound();
        if (entity.Status != "draft")
            throw new BusinessRuleException("QC_LOCKED", "只有草稿验货单可以修改主信息");
        if (input.ReceivingOrderId.HasValue && input.ReceivingOrderId != entity.ReceivingOrderId)
            throw new BusinessRuleException("QC_RECEIVING_IMMUTABLE", "验货单创建后不能更换收货单");

        entity.QcDate = input.QcDate;
        entity.Remark = input.Remark;
        entity.UpdatedBy = CurrentUserId();
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.QcOrders.FindAsync(id);
        if (entity == null) return NotFound();
        if (entity.Status != "draft")
            throw new BusinessRuleException("QC_LOCKED", "已开始验货的单据不能删除");

        entity.IsDeleted = true;
        entity.UpdatedBy = CurrentUserId();
        entity.UpdatedAt = DateTime.Now;
        if (entity.ReceivingOrderId.HasValue)
        {
            var receiving = await _db.ReceivingOrders.FindAsync(entity.ReceivingOrderId.Value);
            if (receiving is not null)
            {
                receiving.Status = "received";
                receiving.UpdatedAt = DateTime.Now;
            }
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private long? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) ? id : null;
    }
}
