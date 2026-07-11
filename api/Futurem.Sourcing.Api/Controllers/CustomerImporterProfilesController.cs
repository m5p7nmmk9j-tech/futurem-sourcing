using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/customer-importer-profiles")]
public class CustomerImporterProfilesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerImporterProfilesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerImporterProfile>>> List([FromQuery] long? customerId)
    {
        var query = _db.CustomerImporterProfiles.AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        return await query
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Take(500)
            .ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CustomerImporterProfile>> Get(long id)
    {
        var entity = await _db.CustomerImporterProfiles.FindAsync(id);
        return entity is null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<CustomerImporterProfile>> Create(CustomerImporterProfile input)
    {
        Validate(input);
        input.Id = 0;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status;
        input.DefaultOriginText = string.IsNullOrWhiteSpace(input.DefaultOriginText)
            ? "Made in China"
            : input.DefaultOriginText.Trim();
        input.CreatedAt = DateTime.Now;

        var hasAny = await _db.CustomerImporterProfiles.AnyAsync(x => x.CustomerId == input.CustomerId);
        if (!hasAny) input.IsDefault = true;
        if (input.IsDefault) await ClearOtherDefaultsAsync(input.CustomerId, null);

        _db.CustomerImporterProfiles.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerImporterProfile>> Update(long id, CustomerImporterProfile input)
    {
        Validate(input);
        var entity = await _db.CustomerImporterProfiles.FindAsync(id);
        if (entity is null) return NotFound();
        if (entity.CustomerId != input.CustomerId)
            throw new BusinessRuleException("IMPORTER_CUSTOMER_IMMUTABLE", "进口商资料所属客户不能修改");

        if (input.IsDefault) await ClearOtherDefaultsAsync(entity.CustomerId, entity.Id);
        entity.Name = input.Name.Trim();
        entity.CompanyName = input.CompanyName.Trim();
        entity.TaxIdOrRfc = input.TaxIdOrRfc?.Trim();
        entity.Address = input.Address.Trim();
        entity.ContactName = input.ContactName?.Trim();
        entity.Phone = input.Phone?.Trim();
        entity.Email = input.Email?.Trim();
        entity.LogoUrl = input.LogoUrl?.Trim();
        entity.DefaultOriginText = string.IsNullOrWhiteSpace(input.DefaultOriginText)
            ? "Made in China"
            : input.DefaultOriginText.Trim();
        entity.DefaultLabelTemplateId = input.DefaultLabelTemplateId;
        entity.DefaultMarkTemplateId = input.DefaultMarkTemplateId;
        entity.IsDefault = input.IsDefault;
        entity.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.CustomerImporterProfiles.FindAsync(id);
        if (entity is null) return NotFound();
        var referenced = await _db.CustomerOrders.AnyAsync(x => x.ImporterProfileId == id && x.Status != "draft");
        if (referenced)
            throw new BusinessRuleException("IMPORTER_IN_USE", "该进口商资料已被确认订单使用，不能删除");

        entity.IsDeleted = true;
        entity.IsDefault = false;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        var next = await _db.CustomerImporterProfiles
            .Where(x => x.CustomerId == entity.CustomerId)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync();
        if (next is not null)
        {
            next.IsDefault = true;
            next.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }
        return Ok(new { ok = true });
    }

    private static void Validate(CustomerImporterProfile input)
    {
        if (input.CustomerId <= 0)
            throw new BusinessRuleException("CUSTOMER_REQUIRED", "请选择客户");
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new BusinessRuleException("IMPORTER_NAME_REQUIRED", "进口商资料名称不能为空");
        if (string.IsNullOrWhiteSpace(input.CompanyName))
            throw new BusinessRuleException("IMPORTER_COMPANY_REQUIRED", "进口商公司名称不能为空");
        if (string.IsNullOrWhiteSpace(input.Address))
            throw new BusinessRuleException("IMPORTER_ADDRESS_REQUIRED", "进口商地址不能为空");
    }

    private async Task ClearOtherDefaultsAsync(long customerId, long? exceptId)
    {
        var defaults = await _db.CustomerImporterProfiles
            .Where(x => x.CustomerId == customerId && x.IsDefault &&
                        (!exceptId.HasValue || x.Id != exceptId.Value))
            .ToListAsync();
        foreach (var item in defaults)
        {
            item.IsDefault = false;
            item.UpdatedAt = DateTime.Now;
        }
    }
}
