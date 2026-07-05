using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/system-settings")]
public class SystemSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SystemSettingsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SystemSetting>>> List([FromQuery] string? group = null)
    {
        var query = _db.SystemSettings.AsQueryable();
        if (!string.IsNullOrWhiteSpace(group)) query = query.Where(x => x.SettingGroup == group);
        return await query.OrderBy(x => x.SettingGroup).ThenBy(x => x.SettingKey).ToListAsync();
    }

    [HttpGet("groups")]
    public ActionResult<IEnumerable<object>> Groups()
    {
        return new object[]
        {
            new { code = "company", name = "公司资料" },
            new { code = "currency", name = "汇率/币种" },
            new { code = "numbering", name = "编号规则" },
            new { code = "logistics", name = "国家/港口/运输" },
            new { code = "payment", name = "付款方式" },
            new { code = "mail", name = "SMTP邮件" },
            new { code = "whatsapp", name = "WhatsApp" },
            new { code = "backup", name = "数据库备份" },
            new { code = "general", name = "系统参数" }
        };
    }

    [HttpPost]
    public async Task<ActionResult<SystemSetting>> Create(SystemSetting input)
    {
        input.Id = 0;
        input.CreatedAt = DateTime.Now;
        _db.SystemSettings.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<SystemSetting>> Update(long id, SystemSetting input)
    {
        var entity = await _db.SystemSettings.FindAsync(id);
        if (entity == null) return NotFound();
        entity.SettingKey = input.SettingKey;
        entity.SettingValue = input.SettingValue;
        entity.SettingGroup = input.SettingGroup;
        entity.ValueType = input.ValueType;
        entity.IsSystem = input.IsSystem;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.SystemSettings.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("set")]
    public async Task<ActionResult<SystemSetting>> Set([FromQuery] string key, [FromQuery] string value, [FromQuery] string group = "general", [FromQuery] string type = "text")
    {
        var entity = await _db.SystemSettings.FirstOrDefaultAsync(x => x.SettingKey == key);
        if (entity == null)
        {
            entity = new SystemSetting { SettingKey = key, SettingValue = value, SettingGroup = group, ValueType = type, CreatedAt = DateTime.Now };
            _db.SystemSettings.Add(entity);
        }
        else
        {
            entity.SettingValue = value;
            entity.SettingGroup = group;
            entity.ValueType = type;
            entity.UpdatedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpPost("seed")]
    public async Task<ActionResult<object>> Seed()
    {
        var defaults = new (string Group, string Key, string Value, string Type, string Remark)[]
        {
            ("company", "company.name", "FUTUREM", "text", "公司名称"),
            ("company", "company.logo", "", "text", "公司Logo地址"),
            ("currency", "currency.base", "USD", "text", "本位币"),
            ("currency", "rate.USD.CNY", "7.20", "number", "美元兑人民币"),
            ("currency", "rate.USD.MXN", "18.00", "number", "美元兑墨西哥比索"),
            ("numbering", "prefix.PO", "PO", "text", "采购单前缀"),
            ("numbering", "prefix.SO", "SO", "text", "销售汇总单前缀"),
            ("numbering", "prefix.PAY", "PAY", "text", "收付款前缀"),
            ("logistics", "ports.default", "NINGBO,SHANGHAI,SHENZHEN", "text", "默认港口"),
            ("logistics", "shipment.modes", "SEA,AIR,EXPRESS,RAIL", "text", "运输方式"),
            ("payment", "payment.methods", "BANK,CASH,ALIPAY,WECHAT,OTHER", "text", "付款方式"),
            ("mail", "smtp.host", "", "text", "SMTP服务器"),
            ("mail", "smtp.port", "587", "number", "SMTP端口"),
            ("whatsapp", "whatsapp.enabled", "false", "bool", "WhatsApp开关"),
            ("backup", "backup.enabled", "false", "bool", "自动备份开关"),
            ("general", "system.language", "zh", "text", "默认语言")
        };
        var created = 0;
        foreach (var item in defaults)
        {
            if (await _db.SystemSettings.AnyAsync(x => x.SettingKey == item.Key)) continue;
            _db.SystemSettings.Add(new SystemSetting { SettingGroup = item.Group, SettingKey = item.Key, SettingValue = item.Value, ValueType = item.Type, Remark = item.Remark, IsSystem = true, CreatedAt = DateTime.Now });
            created++;
        }
        await _db.SaveChangesAsync();
        return new { created, total = await _db.SystemSettings.CountAsync() };
    }
}
