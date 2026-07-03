using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/summary-orders")]
public class SummaryOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    public SummaryOrdersController(AppDbContext db) { _db = db; }

    public record GenerateFromPoRequest(List<long> PurchaseOrderIds, long? CustomerId, string? Currency);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SummaryOrder>>> List([FromQuery] long? customerId)
    {
        var query = _db.SummaryOrders.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SummaryOrder>> Get(long id)
    {
        var entity = await _db.SummaryOrders.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<SummaryOrder>> Create(SummaryOrder input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo("SO") : input.No;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "draft" : input.Status;
        input.ReceivableAmount = input.GoodsAmount + input.CommissionFee + input.WarehouseFee + input.LoadingFee + input.LogisticsFee + input.OtherFee;
        input.CreatedAt = DateTime.Now;
        _db.SummaryOrders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("generate-from-pos")]
    public async Task<ActionResult<SummaryOrder>> GenerateFromPurchaseOrders(GenerateFromPoRequest request)
    {
        if (request.PurchaseOrderIds == null || request.PurchaseOrderIds.Count == 0) return BadRequest("PurchaseOrderIds required");
        var poIds = request.PurchaseOrderIds.Distinct().ToList();
        var pos = await _db.PurchaseOrders.Where(x => poIds.Contains(x.Id)).ToListAsync();
        if (pos.Count == 0) return NotFound();

        var customerId = request.CustomerId ?? pos.FirstOrDefault(x => x.CustomerId.HasValue)?.CustomerId;
        var so = new SummaryOrder
        {
            No = NumberService.NewNo("SO"),
            BuyingTripId = pos.FirstOrDefault()?.BuyingTripId,
            CustomerId = customerId,
            OrderDate = DateTime.Today,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency!,
            Status = "draft",
            Remark = $"由 PO 汇总生成: {string.Join(", ", pos.Select(x => x.No))}",
            CreatedAt = DateTime.Now
        };
        _db.SummaryOrders.Add(so);
        foreach (var po in pos)
        {
            po.Status = "summarized";
            po.UpdatedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();

        foreach (var po in pos) await DocumentLineCopyService.CopyAsync(_db, "PO", po.Id, "SO", so.Id);
        await _db.SaveChangesAsync();

        var lines = await _db.DocumentLines.Where(x => x.DocumentType == "SO" && x.DocumentId == so.Id).ToListAsync();
        so.GoodsAmount = lines.Sum(x => x.Amount);
        so.ReceivableAmount = so.GoodsAmount + so.CommissionFee + so.WarehouseFee + so.LoadingFee + so.LogisticsFee + so.OtherFee;
        so.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return so;
    }

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<SummaryOrder>> Copy(long id)
    {
        var source = await _db.SummaryOrders.FindAsync(id);
        if (source == null) return NotFound();
        var copy = new SummaryOrder
        {
            No = NumberService.NewNo("SO"),
            BuyingTripId = source.BuyingTripId,
            CustomerId = source.CustomerId,
            OrderDate = DateTime.Today,
            Currency = source.Currency,
            Status = "draft",
            GoodsAmount = source.GoodsAmount,
            CommissionFee = source.CommissionFee,
            WarehouseFee = source.WarehouseFee,
            LoadingFee = source.LoadingFee,
            LogisticsFee = source.LogisticsFee,
            OtherFee = source.OtherFee,
            ReceivableAmount = source.ReceivableAmount,
            Remark = $"复制自 {source.No}",
            CreatedAt = DateTime.Now
        };
        _db.SummaryOrders.Add(copy);
        await _db.SaveChangesAsync();
        await DocumentLineCopyService.CopyAsync(_db, "SO", source.Id, "SO", copy.Id);
        await _db.SaveChangesAsync();
        return copy;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<SummaryOrder>> Update(long id, SummaryOrder input)
    {
        var entity = await _db.SummaryOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.CustomerId = input.CustomerId;
        entity.OrderDate = input.OrderDate;
        entity.Currency = input.Currency;
        entity.Status = input.Status;
        entity.GoodsAmount = input.GoodsAmount;
        entity.CommissionFee = input.CommissionFee;
        entity.WarehouseFee = input.WarehouseFee;
        entity.LoadingFee = input.LoadingFee;
        entity.LogisticsFee = input.LogisticsFee;
        entity.OtherFee = input.OtherFee;
        entity.ReceivableAmount = input.GoodsAmount + input.CommissionFee + input.WarehouseFee + input.LoadingFee + input.LogisticsFee + input.OtherFee;
        entity.ReceivedAmount = input.ReceivedAmount;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.SummaryOrders.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
