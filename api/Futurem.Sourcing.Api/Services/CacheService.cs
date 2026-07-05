using System.Text.Json;
using StackExchange.Redis;

namespace Futurem.Sourcing.Api.Services;

public class CacheService
{
    private readonly IConfiguration _config;
    private readonly ILogger<CacheService> _logger;
    private readonly Lazy<Task<IConnectionMultiplexer?>> _connection;

    public CacheService(IConfiguration config, ILogger<CacheService> logger)
    {
        _config = config;
        _logger = logger;
        _connection = new Lazy<Task<IConnectionMultiplexer?>>(ConnectAsync);
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var conn = await _connection.Value;
            return conn?.IsConnected == true;
        }
        catch { return false; }
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var db = await GetDatabaseAsync();
            if (db == null) return default;
            var value = await db.StringGetAsync(key);
            if (!value.HasValue) return default;
            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis get failed for {Key}. Fallback to database.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var db = await GetDatabaseAsync();
            if (db == null) return;
            await db.StringSetAsync(key, JsonSerializer.Serialize(value), expiry ?? TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis set failed for {Key}.", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        var cached = await GetAsync<T>(key);
        if (cached != null) return cached;
        var value = await factory();
        await SetAsync(key, value, expiry);
        return value;
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            var db = await GetDatabaseAsync();
            if (db == null) return;
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis remove failed for {Key}.", key);
        }
    }

    public async Task<long> RemoveKnownPrefixesAsync(IEnumerable<string> prefixes)
    {
        var removed = 0L;
        foreach (var prefix in prefixes)
        {
            try
            {
                var conn = await _connection.Value;
                if (conn == null) continue;
                foreach (var endpoint in conn.GetEndPoints())
                {
                    var server = conn.GetServer(endpoint);
                    var db = conn.GetDatabase();
                    foreach (var key in server.Keys(pattern: prefix + "*"))
                    {
                        if (await db.KeyDeleteAsync(key)) removed++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis remove prefix failed for {Prefix}.", prefix);
            }
        }
        return removed;
    }

    private async Task<IDatabase?> GetDatabaseAsync()
    {
        var conn = await _connection.Value;
        return conn?.IsConnected == true ? conn.GetDatabase() : null;
    }

    private async Task<IConnectionMultiplexer?> ConnectAsync()
    {
        var enabled = bool.TryParse(_config["Redis:Enabled"], out var e) ? e : true;
        if (!enabled) return null;
        var connectionString = _config.GetConnectionString("Redis") ?? _config["Redis:ConnectionString"] ?? "localhost:6379";
        try
        {
            return await ConnectionMultiplexer.ConnectAsync(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable. System will fallback to database.");
            return null;
        }
    }
}
