namespace Futurem.Sourcing.Api.Services;

public static class RmbMoneyService
{
    public const string Currency = "RMB";

    public static string NormalizeCurrency(string? _) => Currency;

    public static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
