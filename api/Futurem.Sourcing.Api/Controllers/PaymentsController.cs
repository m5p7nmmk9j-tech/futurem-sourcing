using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PaymentsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Payment>>> List([FromQuery] string? direction, [FromQuery] long? financeRecordId, [FromQuery] long? customerId, [FromQuery] long? supplierId)
    {
        var query = _db.Payments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(direction)) query = query.Where(x => x.Direction == direction);
        if (financeRecordId.HasValue) query = query.Where(x => x.FinanceRecordId == financeRecordId.Value);
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        return await query.OrderByDescending(x => x.Id).Take(300).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Payment>> Get(long id)
    {
        var entity = await _db.Payments.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Payment>> Create(Payment input)
    {
        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo(input.Direction == "pay" ? "PAY" : "REC") : input.No;
        input.CreatedAt = DateTime.Now;
        _db.Payments.Add(input);

        var finance = await _db.FinanceRecords.FindAsync(input.FinanceRecordId);
        if (finance != null)
        {
            finance.PaidAmount += input.Amount;
            finance.Status = finance.PaidAmount <= 0 ? "pending" : finance.PaidAmount < finance.Amount ? "partial" : "done";
            finance.UpdatedAt = DateTime.Now;
            await SyncSourceDocumentAsync(finance);
        }

        if (input.BankAccountId.HasValue)
        {
            var account = await _db.BankAccounts.FindAsync(input.BankAccountId.Value);
            if (account != null)
            {
                account.CurrentBalance += input.Direction == "pay" ? -input.Amount - input.FeeAmount : input.Amount - input.FeeAmount;
                account.UpdatedAt = DateTime.Now;
            }
        }

        await _db.SaveChangesAsync();
        return input;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Payments.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;

        var finance = await _db.FinanceRecords.FindAsync(entity.FinanceRecordId);
        if (finance != null)
        {
            finance.PaidAmount -= entity.Amount;
            if (finance.PaidAmount < 0) finance.PaidAmount = 0;
            finance.Status = finance.PaidAmount <= 0 ? "pending" : finance.PaidAmount < finance.Amount ? "partial" : "done";
            finance.UpdatedAt = DateTime.Now;
            await SyncSourceDocumentAsync(finance);
        }

        if (entity.BankAccountId.HasValue)
        {
            var account = await _db.BankAccounts.FindAsync(entity.BankAccountId.Value);
            if (account != null)
            {
                account.CurrentBalance -= entity.Direction == "pay" ? -entity.Amount - entity.FeeAmount : entity.Amount - entity.FeeAmount;
                account.UpdatedAt = DateTime.Now;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private async Task SyncSourceDocumentAsync(FinanceRecord finance)
    {
        if (finance.TargetType == "SO")
        {
            var so = await _db.SummaryOrders.FindAsync(finance.TargetId);
            if (so != null)
            {
                so.ReceivedAmount = finance.PaidAmount;
                so.Status = finance.Status == "done" ? "paid" : finance.Status == "partial" ? "partial_paid" : "unpaid";
                so.UpdatedAt = DateTime.Now;
            }
        }
        else if (finance.TargetType == "PO")
        {
            var po = await _db.PurchaseOrders.FindAsync(finance.TargetId);
            if (po != null)
            {
                po.PayStatus = finance.Status == "done" ? "paid" : finance.Status == "partial" ? "partial" : "unpaid";
                po.UpdatedAt = DateTime.Now;
            }
        }
    }
}
