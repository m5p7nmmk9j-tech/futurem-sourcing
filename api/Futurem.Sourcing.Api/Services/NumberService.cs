namespace Futurem.Sourcing.Api.Services;

public static class NumberService
{
    private static readonly object LockObj = new();
    private static string _lastTimestamp = string.Empty;
    private static int _sequence = 0;

    public static string NewNo(string prefix)
    {
        lock (LockObj)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            if (timestamp == _lastTimestamp)
            {
                _sequence++;
            }
            else
            {
                _lastTimestamp = timestamp;
                _sequence = 0;
            }

            return _sequence == 0
                ? $"{prefix}{timestamp}"
                : $"{prefix}{timestamp}{_sequence:00}";
        }
    }

    public static string NewCustomerCode() => NewNo("CUS");
    public static string NewSupplierCode() => NewNo("SUP");
    public static string NewMarketCode() => NewNo("MKT");
    public static string NewProductSku() => NewNo("SKU");
    public static string NewProductBarcode() => NewNo("BAR");
}
