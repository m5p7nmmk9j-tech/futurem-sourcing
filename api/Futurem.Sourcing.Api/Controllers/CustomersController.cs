using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Customer>>> List([FromQuery] string? keyword)
    {
        var query = _db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword) || (x.Whatsapp != null && x.Whatsapp.Contains(keyword)));
        }

        return await query.OrderByDescending(x => x.Id).Take(200).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Customer>> Get(long id)
    {
        var entity = await _db.Customers.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> Create(Customer input)
    {
        input.Id = 0;
        input.Code = string.IsNullOrWhiteSpace(input.Code) ? NumberService.NewCustomerCode() : input.Code;
        input.CreatedAt = DateTime.Now;
        _db.Customers.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<Customer>> Update(long id, Customer input)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = input.Name;
        entity.Country = input.Country;
        entity.Port = input.Port;
        entity.ContactName = input.ContactName;
        entity.Phone = input.Phone;
        entity.Whatsapp = input.Whatsapp;
        entity.Email = input.Email;
        entity.Currency = input.Currency;
        entity.CreditLimit = input.CreditLimit;
        entity.CreditDays = input.CreditDays;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity == null) return NotFound();

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
