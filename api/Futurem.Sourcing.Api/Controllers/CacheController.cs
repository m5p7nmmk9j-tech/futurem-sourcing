using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Futurem.Sourcing.Api.Controllers;

[ApiController]
[Route("api/cache")]
public class CacheController : ControllerBase
{
    private readonly CacheService _cache;
    public CacheController(CacheService cache) { _cache = cache; }

    [HttpGet("status")]
    public async Task<ActionResult<object>> Status()
    {
        return new { available = await _cache.IsAvailableAsync(), provider = "Redis", fallback = "Database" };
    }

    [HttpPost("clear")]
    public async Task<ActionResult<object>> Clear([FromQuery] string scope = "all")
    {
        var prefixes = scope switch
        {
            "dashboard" => new[] { "dashboard:" },
            "bi" => new[] { "bi:" },
            "settings" => new[] { "settings:" },
            "permissions" => new[] { "permissions:" },
            "search" => new[] { "search:" },
            "messages" => new[] { "messages:" },
            _ => new[] { "dashboard:", "bi:", "settings:", "permissions:", "search:", "messages:" }
        };
        var removed = await _cache.RemoveKnownPrefixesAsync(prefixes);
        return new { ok = true, scope, removed };
    }
}
