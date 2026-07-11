using Futurem.Sourcing.Api.Services;

namespace Futurem.Sourcing.Api.Tests.Services;

public class RmbMoneyServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("USD")]
    [InlineData("RMB")]
    public void NormalizeCurrency_AlwaysReturnsRmb(string? input)
        => Assert.Equal("RMB", RmbMoneyService.NormalizeCurrency(input));

    [Fact]
    public void Round_UsesTwoDecimalsAwayFromZero()
        => Assert.Equal(12.35m, RmbMoneyService.Round(12.345m));
}
