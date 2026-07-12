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

    [HttpPost("{id:long}/generate-receivable")]
    public async Task<ActionResult<FinanceRecord>> GenerateReceivable(long id)
    {
        var so = await _db.SummaryOrders.FindAsync(id);
        if (so == null) return NotFound();
        var goodsAmount = await FinanceAutoService.SumDocumentAmountAsync(_db, "SO", so.Id);
        if (goodsAmount > 0) so.GoodsAmount = goodsAmount;
        so.ReceivableAmount = so.GoodsAmount + so.CommissionFee + so.WarehouseFee + so.LoadingFee + so.LogisticsFee + so.OtherFee;
        var finance = await FinanceAutoService.EnsureReceivableAsync(_db, "SO", so.Id, so.CustomerId, so.Currency, so.ReceivableAmount, $"由 SO {so.No} 自动生成应收");
        so.ReceivedAmount = finance.PaidAmount;
        so.Status = finance.Status == "done" ? "paid" : finance.Status == "partial" ? "partial_paid" : so.Status;
        so.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return finance;
    }

    [HttpPost("generate-from-pos")]
    public ActionResult GenerateFromPurchaseOrders(GenerateFromPoRequest request)
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            code = "LEGACY_SUMMARY_GENERATION_REMOVED",
            message = "旧版整张 PO 复制汇总已停用，请在客户汇总单中按 PO 明细和整箱数量加入商品。"
        });
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
