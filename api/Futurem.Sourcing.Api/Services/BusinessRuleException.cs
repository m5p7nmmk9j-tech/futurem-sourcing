namespace Futurem.Sourcing.Api.Services;

public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message, object? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }
    public object? Details { get; }
}
