using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    public AuthController(AppDbContext db, AuthService auth) { _db = db; _auth = auth; }

    public record LoginRequest(string Username, string Password, bool RememberMe = false);
    public record RefreshRequest(string RefreshToken);
    public record ChangePasswordRequest(string OldPassword, string NewPassword);
    public record ResetPasswordRequest(long UserId, string NewPassword);

    [HttpPost("seed-admin")]
    public async Task<ActionResult<object>> SeedAdmin()
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Code == "super_admin");
        if (role == null)
        {
            role = new Role { Code = "super_admin", Name = "超级管理员", DataScope = "all", IsSystem = true, CreatedAt = DateTime.Now };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();
        }
        var user = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Username == "admin");
        if (user == null)
        {
            var hp = _auth.HashPassword("Admin@123456");
            user = new UserAccount { Username = "admin", DisplayName = "系统管理员", RoleId = role.Id, Status = "active", PasswordHash = hp.Hash, PasswordSalt = hp.Salt, PasswordChangedAt = DateTime.Now, CreatedAt = DateTime.Now };
            _db.UserAccounts.Add(user);
            await _db.SaveChangesAsync();
        }
        return new { username = "admin", password = "Admin@123456", role = role.Code };
    }

    [HttpPost("login")]
    public async Task<ActionResult<object>> Login(LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var ua = Request.Headers.UserAgent.ToString();
        var user = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Username == request.Username);
        if (user == null || !_auth.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt) || user.Status != "active")
        {
            _db.LoginLogs.Add(new LoginLog { Username = request.Username, Result = "failed", Message = "用户名、密码错误或账号未启用", IpAddress = ip, UserAgent = ua, CreatedAt = DateTime.Now, LoginAt = DateTime.Now });
            await _db.SaveChangesAsync();
            return Unauthorized(new { message = "用户名、密码错误或账号未启用" });
        }

        var role = user.RoleId.HasValue ? await _db.Roles.FindAsync(user.RoleId.Value) : null;
        var permissionIds = role == null ? new List<long>() : await _db.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync();
        var permissions = await _db.Permissions.Where(x => permissionIds.Contains(x.Id)).Select(x => x.Code).ToArrayAsync();
        var sessionId = Guid.NewGuid().ToString("N");
        var accessToken = _auth.CreateAccessToken(user, permissions, role?.Code, sessionId);
        var refreshToken = _auth.CreateRefreshToken();
        var refreshDays = request.RememberMe ? 30 : 7;
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = _auth.HashToken(refreshToken), ExpiresAt = DateTime.Now.AddDays(refreshDays), CreatedByIp = ip, UserAgent = ua, CreatedAt = DateTime.Now });
        _db.UserSessions.Add(new UserSession { UserId = user.Id, SessionId = sessionId, IpAddress = ip, UserAgent = ua, Status = "online", LoginAt = DateTime.Now, LastSeenAt = DateTime.Now, CreatedAt = DateTime.Now });
        _db.LoginLogs.Add(new LoginLog { UserId = user.Id, Username = user.Username, Result = "success", Message = "登录成功", IpAddress = ip, UserAgent = ua, CreatedAt = DateTime.Now, LoginAt = DateTime.Now });
        user.LastLoginAt = DateTime.Now; user.LastLoginIp = ip; user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return new { accessToken, refreshToken, expiresInMinutes = 60, user = new { user.Id, user.Username, user.DisplayName, user.RoleId }, role, permissions };
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<object>> Refresh(RefreshRequest request)
    {
        var tokenHash = _auth.HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > DateTime.Now);
        if (token == null) return Unauthorized(new { message = "Refresh Token 无效或已过期" });
        var user = await _db.UserAccounts.FindAsync(token.UserId);
        if (user == null || user.Status != "active") return Unauthorized();
        var role = user.RoleId.HasValue ? await _db.Roles.FindAsync(user.RoleId.Value) : null;
        var permissionIds = role == null ? new List<long>() : await _db.RolePermissions.Where(x => x.RoleId == role.Id).Select(x => x.PermissionId).ToListAsync();
        var permissions = await _db.Permissions.Where(x => permissionIds.Contains(x.Id)).Select(x => x.Code).ToArrayAsync();
        var sessionId = Guid.NewGuid().ToString("N");
        return new { accessToken = _auth.CreateAccessToken(user, permissions, role?.Code, sessionId), expiresInMinutes = 60 };
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me()
    {
        var userId = long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        var user = await _db.UserAccounts.FindAsync(userId);
        if (user == null) return NotFound();
        var role = user.RoleId.HasValue ? await _db.Roles.FindAsync(user.RoleId.Value) : null;
        return new { user = new { user.Id, user.Username, user.DisplayName, user.Email, user.Mobile, user.RoleId, user.Status }, role };
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        var rows = await _db.UserSessions.Where(x => x.UserId == userId && x.Status == "online").ToListAsync();
        foreach (var row in rows) { row.Status = "offline"; row.LogoutAt = DateTime.Now; row.UpdatedAt = DateTime.Now; }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        var user = await _db.UserAccounts.FindAsync(userId);
        if (user == null) return NotFound();
        if (!_auth.VerifyPassword(request.OldPassword, user.PasswordHash, user.PasswordSalt)) return BadRequest("旧密码错误");
        var hp = _auth.HashPassword(request.NewPassword);
        user.PasswordHash = hp.Hash; user.PasswordSalt = hp.Salt; user.PasswordChangedAt = DateTime.Now; user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await _db.UserAccounts.FindAsync(request.UserId);
        if (user == null) return NotFound();
        var hp = _auth.HashPassword(request.NewPassword);
        user.PasswordHash = hp.Hash; user.PasswordSalt = hp.Salt; user.PasswordChangedAt = DateTime.Now; user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IEnumerable<UserSession>>> Sessions([FromQuery] long? userId = null)
    {
        var q = _db.UserSessions.AsQueryable();
        if (userId.HasValue) q = q.Where(x => x.UserId == userId.Value);
        return await q.OrderByDescending(x => x.Id).Take(300).ToListAsync();
    }

    [HttpPost("sessions/{id:long}/kick")]
    public async Task<IActionResult> Kick(long id)
    {
        var session = await _db.UserSessions.FindAsync(id);
        if (session == null) return NotFound();
        session.Status = "kicked"; session.LogoutAt = DateTime.Now; session.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("login-logs")]
    public async Task<ActionResult<IEnumerable<LoginLog>>> LoginLogs([FromQuery] string? username = null)
    {
        var q = _db.LoginLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(username)) q = q.Where(x => x.Username == username);
        return await q.OrderByDescending(x => x.Id).Take(500).ToListAsync();
    }
}
