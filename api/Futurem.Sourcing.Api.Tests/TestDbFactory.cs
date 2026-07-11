using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests;

public static class TestDbFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}
