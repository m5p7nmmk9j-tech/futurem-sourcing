using System.Security.Claims;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/customer-summaries")]
public class CustomerSummaryReservationsController : ControllerBase
{
    private static readonly string[] ActiveStatuses = new[] { "draft_reserved", "confirmed" };
    private readonly AppDbContext _db;
    private readonly SummaryReservationService _reservations;

    public CustomerSummaryReservationsController(
        AppDbContext db,
        SummaryReservationService reservations)
    {
        _db = db;
        _reservations = reservations;
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
        var activeItems = await _db.SummaryOrderItems
            .Where(x => ids.Contains(x.SummaryOrderId) && ActiveStatuses.Contains(x.ReservationStatus))
            .ToListAsync();

        var response = summaries.Select(summary =>
        {
            var ownItems = activeItems.Where(x => x.SummaryOrderId == summary.Id).ToList();
            return new
            {
                summary,
                itemCount = ownItems.Count,
                supplierCount = ownItems.Select(x => x.SupplierId).Distinct().Count()
            };
        }).ToList();
        return Ok(response);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var summary = await _db.SummaryOrders.FindAsync(id);
        if (summary == null) return NotFound();
        var items = await BuildRowsAsync(id);
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
        if (summary == null) return NotFound();
        if (summary.Status != "draft")
            throw new BusinessRuleException("SUMMARY_LOCKED", "已确认汇总单不能直接修改");

        if (summary.CustomerId != input.CustomerId)
        {
            var hasItems = await _db.SummaryOrderItems.AnyAsync(x =>
                x.SummaryOrderId == id && ActiveStatuses.Contains(x.ReservationStatus));
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
        var exists = await _db.SummaryOrders.AnyAsync(x => x.Id == id);
        if (!exists) return NotFound();
        return Ok(await BuildRowsAsync(id));
    }

    [HttpGet("{id:long}/available-po-lines")]
    public async Task<IActionResult> AvailablePoLines(long id)
    {
        var summary = await _db.SummaryOrders.FindAsync(id);
        if (summary == null) return NotFound();

        var purchaseOrders = await _db.PurchaseOrders
            .Where(x => x.CustomerId == summary.CustomerId && x.Status == "confirmed")
            .ToListAsync();
        var poIds = purchaseOrders.Select(x => x.Id).ToList();
        var lines = await _db.DocumentLines
            .Where(x => !x.IsDeleted && x.DocumentType == "PO" && poIds.Contains(x.DocumentId) &&
                        x.OrderProductId.HasValue && x.Cartons > 0m && x.CartonQty > 0m)
            .OrderBy(x => x.DocumentId)
            .ThenBy(x => x.SortNo)
            .ToListAsync();
        var lineIds = lines.Select(x => x.Id).ToList();
        var reservations = await _db.SummaryOrderItems
            .Where(x => lineIds.Contains(x.PurchaseOrderLineId) && ActiveStatuses.Contains(x.ReservationStatus))
            .ToListAsync();
        var productIds = lines.Where(x => x.OrderProductId.HasValue)
            .Select(x => x.OrderProductId!.Value)
            .Distinct()
            .ToList();
        var products = await _db.OrderProducts.Where(x => productIds.Contains(x.Id)).ToListAsync();

        var response = new List<object>();
        foreach (var line in lines)
        {
            if (!line.OrderProductId.HasValue) continue;
            var reserved = reservations
                .Where(x => x.PurchaseOrderLineId == line.Id)
                .Sum(x => x.ReservedCartons);
            var available = Math.Max(0m, line.Cartons - reserved);
            if (available <= 0m) continue;
            var product = products.FirstOrDefault(x => x.Id == line.OrderProductId.Value);
            var purchaseOrder = purchaseOrders.FirstOrDefault(x => x.Id == line.DocumentId);
            response.Add(new
            {
                line,
                product,
                purchaseOrder,
                reservedCartons = reserved,
                availableCartons = available,
                availableQuantity = RmbMoneyService.Round(available * line.CartonQty)
            });
        }
        return Ok(response);
    }

    [HttpPost("{id:long}/reserve")]
    public async Task<ActionResult<SummaryOrderItem>> Reserve(long id, ReserveRequest request)
    {
        return await _reservations.ReserveAsync(
            id,
            request.PurchaseOrderLineId,
            request.Cartons,
            CurrentUserId());
    }

    [HttpPost("items/{itemId:long}/release")]
    public async Task<ActionResult<SummaryOrderItem>> Release(long itemId, ReleaseRequest request)
    {
        return await _reservations.ReleaseAsync(itemId, request.Reason, CurrentUserId());
    }

    [HttpPost("{id:long}/confirm")]
    public async Task<ActionResult<SummaryOrder>> Confirm(long id)
    {
        return await _reservations.ConfirmAsync(id, CurrentUserId());
    }

    [HttpPost("{id:long}/append-items")]
    public async Task<ActionResult<SummaryOrder>> Append(long id, AppendRequest request)
    {
        return await _reservations.AppendAsync(id, request.Items, request.Reason, CurrentUserId());
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var summary = await _db.SummaryOrders.FindAsync(id);
        if (summary == null) return NotFound();
        if (summary.Status != "draft")
            throw new BusinessRuleException("SUMMARY_LOCKED", "只有草稿汇总单可以取消");

        var items = await _db.SummaryOrderItems
            .Where(x => x.SummaryOrderId == id && x.ReservationStatus == "draft_reserved")
            .ToListAsync();
        foreach (var item in items)
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

    private async Task<List<object>> BuildRowsAsync(long summaryOrderId)
    {
        var items = await _db.SummaryOrderItems
            .Where(x => x.SummaryOrderId == summaryOrderId)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var lineIds = items.Select(x => x.PurchaseOrderLineId).Distinct().ToList();
        var productIds = items.Select(x => x.OrderProductId).Distinct().ToList();
        var poIds = items.Select(x => x.PurchaseOrderId).Distinct().ToList();
        var lines = await _db.DocumentLines.Where(x => lineIds.Contains(x.Id)).ToListAsync();
        var products = await _db.OrderProducts.Where(x => productIds.Contains(x.Id)).ToListAsync();
        var purchaseOrders = await _db.PurchaseOrders.Where(x => poIds.Contains(x.Id)).ToListAsync();

        var response = new List<object>();
        foreach (var item in items)
        {
            response.Add(new
            {
                item,
                line = lines.FirstOrDefault(x => x.Id == item.PurchaseOrderLineId),
                product = products.FirstOrDefault(x => x.Id == item.OrderProductId),
                purchaseOrder = purchaseOrders.FirstOrDefault(x => x.Id == item.PurchaseOrderId)
            });
        }
        return response;
    }

    private long? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        long parsed;
        return long.TryParse(raw, out parsed) ? parsed : null;
    }
}
