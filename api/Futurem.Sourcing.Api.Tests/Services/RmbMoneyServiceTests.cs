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

    [Theory]
    [InlineData(12.345, 12.35)]
    [InlineData(-12.345, -12.35)]
    public void Round_UsesTwoDecimalsAwayFromZero(decimal input, decimal expected)
        => Assert.Equal(expected, RmbMoneyService.Round(input));
}
