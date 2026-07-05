using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditLogsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLog>>> List([FromQuery] string? module = null, [FromQuery] string? action = null, [FromQuery] string? targetType = null, [FromQuery] long? userId = null, [FromQuery] DateTime? start = null, [FromQuery] DateTime? end = null)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(x => x.Module == module);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action == action);
        if (!string.IsNullOrWhiteSpace(targetType)) query = query.Where(x => x.TargetType == targetType);
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (start.HasValue) query = query.Where(x => x.CreatedAt.Date >= start.Value.Date);
        if (end.HasValue) query = query.Where(x => x.CreatedAt.Date <= end.Value.Date);
        return await query.OrderByDescending(x => x.Id).Take(500).ToListAsync();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary()
    {
        var today = DateTime.Today;
        var rows = await _db.AuditLogs.Where(x => x.CreatedAt.Date == today).ToListAsync();
        return new
        {
            today = rows.Count,
            creates = rows.Count(x => x.Action == "create"),
            updates = rows.Count(x => x.Action == "update"),
            deletes = rows.Count(x => x.Action == "delete"),
            approvals = rows.Count(x => x.Action == "approve" || x.Action == "reject" || x.Action == "return"),
            failures = rows.Count(x => x.Result != "success")
        };
    }

    [HttpPost]
    public async Task<ActionResult<AuditLog>> Create(AuditLog input)
    {
        input.Id = 0;
        input.CreatedAt = DateTime.Now;
        input.IpAddress = string.IsNullOrWhiteSpace(input.IpAddress) ? HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty : input.IpAddress;
        input.UserAgent = string.IsNullOrWhiteSpace(input.UserAgent) ? Request.Headers.UserAgent.ToString() : input.UserAgent;
        _db.AuditLogs.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPost("quick")]
    public async Task<ActionResult<AuditLog>> Quick([FromQuery] string module, [FromQuery] string action, [FromQuery] string targetType, [FromQuery] long? targetId = null, [FromQuery] string? targetNo = null, [FromQuery] long? userId = null, [FromQuery] string? username = null)
    {
        var log = new AuditLog
        {
            Module = module,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetNo = targetNo ?? string.Empty,
            UserId = userId,
            Username = username ?? string.Empty,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = Request.Headers.UserAgent.ToString(),
            Result = "success",
            CreatedAt = DateTime.Now
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var entity = await _db.AuditLogs.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
