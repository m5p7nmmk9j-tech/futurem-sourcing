using Futurem.Sourcing.Api.Entities;

namespace Futurem.Sourcing.Api.Services;

public static class FinanceBalanceService
{
    public static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal EffectiveSettled(FinanceRecord record)
        => Round2(record.PaidAmount + record.PrepaymentAppliedAmount - record.OverpaymentTransferredAmount);

    public static decimal Outstanding(FinanceRecord record)
        => Math.Max(0m, Round2(record.Amount - EffectiveSettled(record)));

    public static void RefreshStatus(FinanceRecord record)
    {
        record.Amount = Round2(record.Amount);
        record.PaidAmount = Round2(record.PaidAmount);
        record.PrepaymentAppliedAmount = Round2(record.PrepaymentAppliedAmount);
        record.OverpaymentTransferredAmount = Round2(record.OverpaymentTransferredAmount);

        if (record.Amount <= 0m)
        {
            record.Status = "done";
            return;
        }

        var settled = EffectiveSettled(record);
        record.Status = settled <= 0m ? "pending" : settled < record.Amount ? "partial" : "done";
    }
}
