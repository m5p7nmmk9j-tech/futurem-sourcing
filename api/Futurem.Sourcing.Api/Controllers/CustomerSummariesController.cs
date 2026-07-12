using System.Security.Claims;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/customer-summaries")]
public class CustomerSummariesController : ControllerBase
{
    private static readonly string[] ActiveReservationStatuses = ["draft_reserved", "confirmed"];

    private readonly AppDbContext _db;
    private readonly SummaryReservationService _reservations;
    private readonly DeliveryNoticeService _deliveryNotices;

    public CustomerSummariesController(
        AppDbContext db,
        SummaryReservationService reservations,
        DeliveryNoticeService deliveryNotices)
    {
        _db = db;
        _reservations = reservations;
        _deliveryNotices = deliveryNotices;
    }

    public sealed record ReserveRequest(long PurchaseOrderLineId, decimal Cartons);
    public sealed record ReleaseRequest(string Reason);
    public sealed record AppendRequest(List<SummaryAppendItem> Items, string Reason);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] long? customerId)
    {
        var query = _db.SummaryOrders.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        var summaries = await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
        var ids = summaries.Select(x => x.Id).ToList();
        var itemCounts = await _db.SummaryOrderItems
            .Where(x => ids.Contains(x.SummaryOrderId) && ActiveReservationStatuses.Contains(x.ReservationStatus))
            .GroupBy(x => x.SummaryOrderId)
            .Select(x => new { SummaryOrderId = x.Key, Count = x.Count(), SupplierCount = x.Select(y => y.SupplierId).Distinct().Count() })
            .ToListAsync();
        var counts = itemCounts.ToDictionary(x => x.SummaryOrderId);

        return Ok(summaries.Select(summary => new
        {
            summary,
            itemCount = counts.GetValueOrDefault(summary.Id)?.Count ?? 0,
            supplierCount = counts.GetValueOrDefault(summary.Id)?.SupplierCount ?? 0
        }));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var summary = await _db.SummaryOrders.FindAsync(id);
        if (summary is null) return NotFound();
        var items = await BuildItemRowsAsync(id);
        return Ok(new { summary, items });
    }

    [HttpPost]
    public async Task<ActionResult<SummaryOrder>> Create(SummaryOrder input)
    {
        if (input.CustomerId <= 0)
            throw new BusinessRuleException("CUSTOMER_REQUIRED", "请选择客户");
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("SUM") : input.No.Trim();
        input.Currency = RmbMoneyService.Currency;
        input.Status = "draft";
        input.ConfirmedAt = null;
        input.GoodsAmount = 0m;
        input.ReceivableAmount = 0m;
        input.ReceivedAmount = 0m;
        input.CreatedAt = DateTime.Now;
        _db.SummaryOrders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<SummaryOrder>> Update(long id, SummaryOrder input)
    {
        var summary = await _db.SummaryOrders.FindAsync(id);
        if (summary is null) return NotFound();
        if (summary.Status != "draft")
            throw new BusinessRuleException("SUMMARY_LOCKED", "已确认汇总单不能直接修改");
        if (summary.CustomerId != input.CustomerId)
        {
            var hasItems = await _db.SummaryOrderItems
                .AnyAsync(x => x.SummaryOrderId == id && ActiveReservationStatuses.Contains(x.ReservationStatus));
            if (hasItems)
                throw new BusinessRuleException("SUMMARY_CUSTOMER_IMMUTABLE", "已有汇总商品时不能更换客户");
        }

        summary.CustomerId = input.CustomerId;
        summary.OrderDate = input.OrderDate;
        summary.ContainerType = input.ContainerType;
        summary.WarehouseId = input.WarehouseId;
        summary.PlannedDeliveryDate = input.PlannedDeliveryDate;
        summary.Currency = RmbMoneyService.Currency;
        summary.Remark = input.Remark;
        summary.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return summary;
    }

    [HttpGet("{id:long}/items")]
    public async Task<IActionResult> Items(long id)
    {
        if (!await _db.SummaryOrders.AnyAsync(x => x.Id == id)) return NotFound();
        return Ok(await BuildItemRowsAsync(id));
    }

    [HttpGet("{id:long}/available-po-lines")]
    public async Task<IActionResult> AvailablePoLines(long id)
    {
        var summary = await _db.SummaryOrders.FindAsync(id);
        if (summary is null) return NotFound();

        var confirmedPoIds = await _db.PurchaseOrders
            .Where(x => x.CustomerId == summary.CustomerId && x.Status == "confirmed")
            .Select(x => x.Id)
            .ToListAsync();
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "PO" && confirmedPoIds.Contains(x.DocumentId) &&
                        x.OrderProductId.HasValue && x.Cartons > 0 && x.CartonQty > 0)
            .OrderBy(x => x.DocumentId)
            .ThenBy(x => x.SortNo)
            .ToListAsync();
        var lineIds = lines.Select(x => x.Id).ToList();
        var reservations = await _db.SummaryOrderItems
            .Where(x => lineIds.Contains(x.PurchaseOrderLineId) && ActiveReservationStatuses.Contains(x.ReservationStatus))
            .GroupBy(x => x.PurchaseOrderLineId)
            .Select(x => new { LineId = x.Key, ReservedCartons = x.Sum(y => y.ReservedCartons) })
            .ToListAsync();
        var reservedByLine = reservations.ToDictionary(x => x.LineId, x => x.ReservedCartons);
        var productIds = lines.Select(x => x.OrderProductId!.Value).Distinct().ToList();
        var products = await _db.OrderProducts.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var poById = await _db.PurchaseOrders.Where(x => confirmedPoIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        return Ok(lines.Select(line =>
        {
            var reserved = reservedByLine.GetValueOrDefault(line.Id);
            var available = Math.Max(0m, line.Cartons - reserved);
            var product = products.GetValueOrDefault(line.OrderProductId!.Value);
            var po = poById.GetValueOrDefault(line.DocumentId);
            return new
            {
                line,
                product,
                purchaseOrder = po,
                reservedCartons = reserved,
                availableCartons = available,
                availableQuantity = RmbMoneyService.Round(available * line.CartonQty)
            };
        }).Where(x => x.availableCartons > 0));
    }

    [HttpPost("{id:long}/reserve")]
    public async Task<ActionResult<SummaryOrderItem>> Reserve(long id, ReserveRequest request)
        => await _reservations.ReserveAsync(id, request.PurchaseOrderLineId, request.Cartons, CurrentUserId());

    [HttpPost("items/{itemId:long}/release")]
    public async Task<ActionResult<SummaryOrderItem>> Release(long itemId, ReleaseRequest request)
        => await _reservations.ReleaseAsync(itemId, request.Reason, CurrentUserId());

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id)
    {
        var draft = await _db.SummaryOrders.FindAsync(id);
        if (draft is null) return NotFound();
        if (!draft.WarehouseId.HasValue || draft.WarehouseId.Value <= 0 || !draft.PlannedDeliveryDate.HasValue)
            throw new BusinessRuleException("SUMMARY_DELIVERY_PLAN_REQUIRED", "确认汇总单前必须选择计划送货日期和收货仓库");

        var userId = CurrentUserId();
        var summary = await _reservations.ConfirmAsync(id, userId);
        var notices = await _deliveryNotices.GenerateForConfirmedSummaryAsync(
            summary.Id,
            summary.PlannedDeliveryDate.Value,
            summary.WarehouseId.Value,
            userId);
        return Ok(new { summary, deliveryNotices = notices });
    }

    [HttpPost("{id:long}/append-items")]
    public async Task<ActionResult<SummaryOrder>> Append(long id, AppendRequest request)
        => await _reservations.AppendAsync(id, request.Items, request.Reason, CurrentUserId());

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var summary = await _db.SummaryOrders.FindAsync(id);
        if (summary is null) return NotFound();
        if (summary.Status != "draft")
            throw new BusinessRuleException("SUMMARY_LOCKED", "只有草稿汇总单可以取消");

        var activeItems = await _db.SummaryOrderItems
            .Where(x => x.SummaryOrderId == id && x.ReservationStatus == "draft_reserved")
            .ToListAsync();
        foreach (var item in activeItems)
        {
            item.ReservationStatus = "released";
            item.ReleasedAt = DateTime.Now;
            item.ReleaseReason = "汇总单取消";
            item.UpdatedAt = DateTime.Now;
        }
        summary.Status = "cancelled";
        summary.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private async Task<List<object>> BuildItemRowsAsync(long summaryOrderId)
    {
        var items = await _db.SummaryOrderItems
            .Where(x => x.SummaryOrderId == summaryOrderId)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var lineIds = items.Select(x => x.PurchaseOrderLineId).Distinct().ToList();
        var lines = await _db.DocumentLines.Where(x => lineIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var productIds = items.Select(x => x.OrderProductId).Distinct().ToList();
        var products = await _db.OrderProducts.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var poIds = items.Select(x => x.PurchaseOrderId).Distinct().ToList();
        var pos = await _db.PurchaseOrders.Where(x => poIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        return items.Select(item => (object)new
        {
            item,
            line = lines.GetValueOrDefault(item.PurchaseOrderLineId),
            product = products.GetValueOrDefault(item.OrderProductId),
            purchaseOrder = pos.GetValueOrDefault(item.PurchaseOrderId)
        }).ToList();
    }

    private long? CurrentUserId()
    {
        var principal = ControllerContext?.HttpContext?.User;
        if (principal is null) return null;
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return long.TryParse(raw, out var id) ? id : null;
    }
}
