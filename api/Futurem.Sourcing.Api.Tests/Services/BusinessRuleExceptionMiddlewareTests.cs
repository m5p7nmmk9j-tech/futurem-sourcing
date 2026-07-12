using System.Text.Json;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Http;

namespace Futurem.Sourcing.Api.Tests.Services;

public class BusinessRuleExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WritesStructuredBadRequest()
    {
        var middleware = new BusinessRuleExceptionMiddleware(_ =>
            throw new BusinessRuleException("ORDER_LOCKED", "订单已确认", new { orderId = 42 }));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("ORDER_LOCKED", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("订单已确认", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(42, json.RootElement.GetProperty("details").GetProperty("orderId").GetInt32());
    }
}
