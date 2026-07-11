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
    private readonly SupplierPrepaymentService _prepaymentService;

    public PaymentsController(AppDbContext db, SupplierPrepaymentService prepaymentService)
    {
        _db = db;
        _prepaymentService = prepaymentService;
    }

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
        input.Amount = FinanceBalanceService.Round2(input.Amount);
        input.FeeAmount = FinanceBalanceService.Round2(input.FeeAmount);
        if (input.Amount <= 0m) return BadRequest(new { message = "付款或收款金额必须大于 0" });

        var finance = await _db.FinanceRecords.FindAsync(input.FinanceRecordId);
        if (finance == null) return BadRequest(new { message = "财务记录不存在" });
        var outstanding = FinanceBalanceService.Outstanding(finance);
        if (input.Amount > outstanding)
            return BadRequest(new { message = $"金额不能超过未收/未付余额 {outstanding:F2}" });

        input.Id = 0;
        input.No = string.IsNullOrWhiteSpace(input.No) ? NumberService.NewNo(input.Direction == "pay" ? "PAY" : "REC") : input.No;
        input.TargetType = finance.TargetType;
        input.TargetId = finance.TargetId;
        input.CustomerId = finance.CustomerId;
        input.SupplierId = finance.SupplierId;
        input.Currency = finance.Currency;
        input.CreatedAt = DateTime.Now;
        _db.Payments.Add(input);

        finance.PaidAmount = FinanceBalanceService.Round2(finance.PaidAmount + input.Amount);
        FinanceBalanceService.RefreshStatus(finance);
        finance.UpdatedAt = DateTime.Now;
        await SyncSourceDocumentAsync(finance);

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
            finance.PaidAmount = Math.Max(0m, FinanceBalanceService.Round2(finance.PaidAmount - entity.Amount));
            if (finance.TargetType == "SHIPMENT_EXPENSE")
            {
                var desiredTransfer = Math.Max(0m, FinanceBalanceService.Round2(finance.PaidAmount - finance.Amount));
                await _prepaymentService.UpsertOverpaymentAsync(finance, desiredTransfer);
            }
            FinanceBalanceService.RefreshStatus(finance);
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
        else if (finance.TargetType == "SHIPMENT_EXPENSE" && finance.ShipmentExpenseId.HasValue)
        {
            var expense = await _db.ShipmentExpenses.FindAsync(finance.ShipmentExpenseId.Value);
            if (expense != null)
            {
                expense.FinanceStatus = finance.Status;
                expense.UpdatedAt = DateTime.Now;
            }
        }
    }
}
