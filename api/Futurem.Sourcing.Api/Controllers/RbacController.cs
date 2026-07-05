using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/rbac")]
public class RbacController : ControllerBase
{
    private readonly AppDbContext _db;
    public RbacController(AppDbContext db) { _db = db; }

    public record AssignPermissionsRequest(List<long> PermissionIds);

    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<Role>>> Roles() => await _db.Roles.OrderBy(x => x.Id).ToListAsync();

    [HttpPost("roles")]
    public async Task<ActionResult<Role>> CreateRole(Role input)
    {
        input.Id = 0;
        input.CreatedAt = DateTime.Now;
        _db.Roles.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("roles/{id:long}")]
    public async Task<ActionResult<Role>> UpdateRole(long id, Role input)
    {
        var entity = await _db.Roles.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.DataScope = input.DataScope;
        entity.IsSystem = input.IsSystem;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("roles/{id:long}")]
    public async Task<IActionResult> DeleteRole(long id)
    {
        var entity = await _db.Roles.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<IEnumerable<Permission>>> Permissions([FromQuery] string? module = null)
    {
        var query = _db.Permissions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(x => x.Module == module);
        return await query.OrderBy(x => x.Module).ThenBy(x => x.Code).ToListAsync();
    }

    [HttpPost("permissions")]
    public async Task<ActionResult<Permission>> CreatePermission(Permission input)
    {
        input.Id = 0;
        input.CreatedAt = DateTime.Now;
        _db.Permissions.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("permissions/{id:long}")]
    public async Task<ActionResult<Permission>> UpdatePermission(long id, Permission input)
    {
        var entity = await _db.Permissions.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Code = input.Code;
        entity.Name = input.Name;
        entity.Module = input.Module;
        entity.PermissionType = input.PermissionType;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("permissions/{id:long}")]
    public async Task<IActionResult> DeletePermission(long id)
    {
        var entity = await _db.Permissions.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserAccount>>> Users([FromQuery] long? roleId = null, [FromQuery] string? status = null)
    {
        var query = _db.UserAccounts.AsQueryable();
        if (roleId.HasValue) query = query.Where(x => x.RoleId == roleId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.Id).ToListAsync();
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserAccount>> CreateUser(UserAccount input)
    {
        input.Id = 0;
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status;
        input.CreatedAt = DateTime.Now;
        _db.UserAccounts.Add(input);
        await _db.SaveChangesAsync();
        return input;
    }

    [HttpPut("users/{id:long}")]
    public async Task<ActionResult<UserAccount>> UpdateUser(long id, UserAccount input)
    {
        var entity = await _db.UserAccounts.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Username = input.Username;
        entity.DisplayName = input.DisplayName;
        entity.Email = input.Email;
        entity.Mobile = input.Mobile;
        entity.RoleId = input.RoleId;
        entity.CompanyId = input.CompanyId;
        entity.StoreId = input.StoreId;
        entity.WarehouseId = input.WarehouseId;
        entity.Status = input.Status;
        entity.Remark = input.Remark;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("users/{id:long}")]
    public async Task<IActionResult> DeleteUser(long id)
    {
        var entity = await _db.UserAccounts.FindAsync(id);
        if (entity == null) return NotFound();
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("roles/{roleId:long}/permissions")]
    public async Task<ActionResult<object>> RolePermissions(long roleId)
    {
        var role = await _db.Roles.FindAsync(roleId);
        if (role == null) return NotFound();
        var permissionIds = await _db.RolePermissions.Where(x => x.RoleId == roleId).Select(x => x.PermissionId).ToListAsync();
        var permissions = await _db.Permissions.Where(x => permissionIds.Contains(x.Id)).OrderBy(x => x.Module).ThenBy(x => x.Code).ToListAsync();
        return new { role, permissionIds, permissions };
    }

    [HttpPost("roles/{roleId:long}/permissions")]
    public async Task<IActionResult> AssignPermissions(long roleId, AssignPermissionsRequest request)
    {
        var role = await _db.Roles.FindAsync(roleId);
        if (role == null) return NotFound();
        var old = await _db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync();
        foreach (var item in old) item.IsDeleted = true;
        foreach (var permissionId in request.PermissionIds.Distinct())
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId, CreatedAt = DateTime.Now });
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, count = request.PermissionIds.Distinct().Count() });
    }

    [HttpGet("users/{userId:long}/profile")]
    public async Task<ActionResult<object>> UserProfile(long userId)
    {
        var user = await _db.UserAccounts.FindAsync(userId);
        if (user == null) return NotFound();
        var role = user.RoleId.HasValue ? await _db.Roles.FindAsync(user.RoleId.Value) : null;
        var permissionIds = role == null ? new List<long>() : await _db.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync();
        var permissions = await _db.Permissions.Where(x => permissionIds.Contains(x.Id)).Select(x => x.Code).ToListAsync();
        return new { user, role, permissions, dataScope = role?.DataScope ?? "self" };
    }

    [HttpPost("seed")]
    public async Task<ActionResult<object>> Seed()
    {
        string[] roleCodes = { "super_admin", "boss", "manager", "purchase_manager", "purchase", "qc", "warehouse", "finance", "sales", "service" };
        foreach (var code in roleCodes)
        {
            if (!await _db.Roles.AnyAsync(x => x.Code == code)) _db.Roles.Add(new Role { Code = code, Name = code, DataScope = code == "super_admin" || code == "boss" ? "all" : "self", IsSystem = true, CreatedAt = DateTime.Now });
        }
        string[] modules = { "dashboard", "bi", "products", "customers", "suppliers", "rfq", "co", "po", "receiving", "qc", "container", "shipment", "finance", "bank", "message", "approval", "rbac" };
        string[] actions = { "view", "create", "edit", "delete", "export", "approve" };
        foreach (var module in modules)
        foreach (var action in actions)
        {
            var code = $"{module}.{action}";
            if (!await _db.Permissions.AnyAsync(x => x.Code == code)) _db.Permissions.Add(new Permission { Code = code, Name = code, Module = module, PermissionType = action == "view" ? "page" : "button", CreatedAt = DateTime.Now });
        }
        await _db.SaveChangesAsync();
        return new { roles = await _db.Roles.CountAsync(), permissions = await _db.Permissions.CountAsync() };
    }
}
