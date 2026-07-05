using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Futurem.Sourcing.Api.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Futurem.Sourcing.Api.Services;

public class AuthService
{
    private readonly IConfiguration _config;
    public AuthService(IConfiguration config) { _config = config; }

    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 120000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt)) return false;
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 120000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hashBytes, Convert.FromBase64String(hash));
    }

    public string CreateAccessToken(UserAccount user, string[] permissions, string? roleCode, string sessionId)
    {
        var key = _config["Jwt:Key"] ?? "FUTUREM_ENTERPRISE_DEV_SECRET_CHANGE_ME_32_CHARS";
        var issuer = _config["Jwt:Issuer"] ?? "FUTUREM";
        var audience = _config["Jwt:Audience"] ?? "FUTUREM_WEB";
        var minutes = int.TryParse(_config["Jwt:AccessMinutes"], out var m) ? m : 60;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("displayName", user.DisplayName),
            new("roleId", user.RoleId?.ToString() ?? string.Empty),
            new("roleCode", roleCode ?? string.Empty),
            new("sessionId", sessionId)
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddMinutes(minutes), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));
    }
}
