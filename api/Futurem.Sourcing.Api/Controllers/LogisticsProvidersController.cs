using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/logistics-providers")]
public sealed class LogisticsProvidersController : ControllerBase
{
    private readonly AppDbContext _db;

    public LogisticsProvidersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LogisticsProvider>>> List(
        [FromQuery] string? keyword,
        [FromQuery] string? serviceType,
        [FromQuery] string? status)
    {
        var query = _db.LogisticsProviders.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(x => x.Code.Contains(value) || x.Name.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(serviceType))
            query = query.Where(x => x.ServiceTypesJson.Contains(serviceType.Trim()));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status.Trim());
        return await query.OrderBy(x => x.Code).Take(500).ToListAsync();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<LogisticsProvider>> Get(long id)
    {
        var entity = await _db.LogisticsProviders.FindAsync(id);
        return entity is null ? NotFound() : entity;
    }

    [HttpPost]
    public async Task<ActionResult<LogisticsProvider>> Create(LogisticsProvider input)
    {
        Normalize(input);
        if (await _db.LogisticsProviders.AnyAsync(x => x.Code == input.Code))
            throw new BusinessRuleException("LOGISTICS_PROVIDER_CODE_EXISTS", "物流服务商编码已存在");
        input.Id = 0;
        input.CreatedAt = DateTime.Now;
        _db.LogisticsProviders.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<LogisticsProvider>> Update(long id, LogisticsProvider input)
    {
        var entity = await _db.LogisticsProviders.FindAsync(id);
        if (entity is null) return NotFound();
        Normalize(input);
        if (await _db.LogisticsProviders.AnyAsync(x => x.Id != id && x.Code == input.Code))
            throw new BusinessRuleException("LOGISTICS_PROVIDER_CODE_EXISTS", "物流服务商编码已存在");
        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.ServiceTypesJson = input.ServiceTypesJson;
        entity.ContactName = input.ContactName;
        entity.Phone = input.Phone;
        entity.Email = input.Email;
        entity.Address = input.Address;
        entity.TaxId = input.TaxId;
        entity.BankInfoJson = input.BankInfoJson;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    private static void Normalize(LogisticsProvider provider)
    {
        provider.Code = provider.Code.Trim().ToUpperInvariant();
        provider.Name = provider.Name.Trim();
        provider.ServiceTypesJson = string.IsNullOrWhiteSpace(provider.ServiceTypesJson) ? "[]" : provider.ServiceTypesJson.Trim();
        provider.Status = string.IsNullOrWhiteSpace(provider.Status) ? "active" : provider.Status.Trim();
        if (string.IsNullOrWhiteSpace(provider.Code) || string.IsNullOrWhiteSpace(provider.Name))
            throw new BusinessRuleException("LOGISTICS_PROVIDER_REQUIRED_FIELDS", "物流服务商编码和名称不能为空");
    }
}
