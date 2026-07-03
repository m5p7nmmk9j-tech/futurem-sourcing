using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/bank-accounts")]
public class BankAccountsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BankAccountsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BankAccount>>> List([FromQuery] string? keyword, [FromQuery] string? currency)
    {
        var query = _db.BankAccounts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword) || (x.BankName != null && x.BankName.Contains(keyword)));
        if (!string.IsNullOrWhiteSpace(currency)) query = query.Where(x => x.Currency == currency);
        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BankAccount>> Get(long id)
    {
        var entity = await _db.BankAccounts.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<BankAccount>> Create(BankAccount input)
    {
        input.Id = 0;
        input.Code = string.IsNullOrWhiteSpace(input.Code) ? NumberService.NewNo("ACC") : input.Code;
        input.CurrentBalance = input.OpeningBalance;
        input.CreatedAt = DateTime.Now;
        _db.BankAccounts.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<BankAccount>> Update(long id, BankAccount input)
    {
        var entity = await _db.BankAccounts.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Name = input.Name;
        entity.BankName = input.BankName;
        entity.AccountNo = input.AccountNo;
        entity.Currency = input.Currency;
        entity.OpeningBalance = input.OpeningBalance;
        entity.CurrentBalance = input.CurrentBalance;
        entity.IsDefault = input.IsDefault;
        entity.IsActive = input.IsActive;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.BankAccounts.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
