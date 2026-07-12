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
        input.Status = NormalizeStatus(input.Status);
        input.DefaultOriginText = NormalizeOrigin(input.DefaultOriginText);
        input.CreatedAt = DateTime.Now;

        var hasAnyActive = await _db.CustomerImporterProfiles
            .AnyAsync(x => x.CustomerId == input.CustomerId && x.Status == "active");
        if (!hasAnyActive) input.IsDefault = true;
        if (input.IsDefault)
        {
            input.Status = "active";
            await ClearOtherDefaultsAsync(input.CustomerId, null);
        }

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

        var requestedStatus = NormalizeStatus(input.Status);
        var requestedDefault = input.IsDefault;
        if (requestedDefault)
        {
            requestedStatus = "active";
            await ClearOtherDefaultsAsync(entity.CustomerId, entity.Id);
        }
        else if (entity.IsDefault)
        {
            var replacement = await _db.CustomerImporterProfiles
                .Where(x => x.CustomerId == entity.CustomerId && x.Id != entity.Id && x.Status == "active")
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();
            if (replacement is null)
            {
                requestedDefault = true;
                requestedStatus = "active";
            }
            else
            {
                replacement.IsDefault = true;
                replacement.UpdatedAt = DateTime.Now;
            }
        }

        entity.Name = input.Name.Trim();
        entity.CompanyName = input.CompanyName.Trim();
        entity.TaxIdOrRfc = input.TaxIdOrRfc?.Trim();
        entity.Address = input.Address.Trim();
        entity.ContactName = input.ContactName?.Trim();
        entity.Phone = input.Phone?.Trim();
        entity.Email = input.Email?.Trim();
        entity.LogoUrl = input.LogoUrl?.Trim();
        entity.DefaultOriginText = NormalizeOrigin(input.DefaultOriginText);
        entity.DefaultLabelTemplateId = input.DefaultLabelTemplateId;
        entity.DefaultMarkTemplateId = input.DefaultMarkTemplateId;
        entity.IsDefault = requestedDefault;
        entity.Status = requestedStatus;
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

        var referencedByCustomerOrder = await _db.CustomerOrders.AnyAsync(x => x.ImporterProfileId == id);
        var referencedByPurchaseOrder = await _db.PurchaseOrders.AnyAsync(x => x.ImporterProfileId == id);
        var referencedByOrderProduct = await _db.OrderProducts.AnyAsync(x => x.ImporterProfileId == id);
        if (referencedByCustomerOrder || referencedByPurchaseOrder || referencedByOrderProduct)
            throw new BusinessRuleException("IMPORTER_IN_USE", "该进口商资料已被业务单据使用，不能删除");

        var wasDefault = entity.IsDefault;
        entity.IsDeleted = true;
        entity.IsDefault = false;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        if (wasDefault)
        {
            var next = await _db.CustomerImporterProfiles
                .Where(x => x.CustomerId == entity.CustomerId && x.Status == "active")
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();
            if (next is not null)
            {
                next.IsDefault = true;
                next.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
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

    private static string NormalizeStatus(string? status)
        => string.Equals(status?.Trim(), "inactive", StringComparison.OrdinalIgnoreCase)
            ? "inactive"
            : "active";

    private static string NormalizeOrigin(string? origin)
        => string.IsNullOrWhiteSpace(origin) ? "Made in China" : origin.Trim();
}
