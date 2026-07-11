using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;

namespace Futurem.Sourcing.Api.Tests;

public class ShipmentFinanceServicesTests
{
    [Fact]
    public void Outstanding_UsesCashCreditsAndTransferredOverpayment()
    {
        var record = new FinanceRecord
        {
            Amount = 10000m,
            PaidAmount = 6000m,
            PrepaymentAppliedAmount = 2000m,
            OverpaymentTransferredAmount = 500m
        };
        Assert.Equal(2500m, FinanceBalanceService.Outstanding(record));
    }

    [Fact]
    public async Task Measurement_SumsLinesToTwoDecimalsAndKeepsManualFinalValue()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-1", FinalTotalCbm = 9m };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        db.DocumentLines.AddRange(
            new DocumentLine { DocumentType = "SHP", DocumentId = shipment.Id, TotalCbm = 1.234m, TotalGwKg = 100.555m, TotalNwKg = 90.444m },
            new DocumentLine { DocumentType = "SHP", DocumentId = shipment.Id, TotalCbm = 2.345m, TotalGwKg = 50.555m, TotalNwKg = 40.444m });
        await db.SaveChangesAsync();

        var result = await new ShipmentMeasurementService(db).RecalculateAsync(shipment.Id, false);
        Assert.Equal(3.58m, result.CalculatedTotalCbm);
        Assert.Equal(151.11m, result.CalculatedGrossWeightKg);
        Assert.Equal(130.89m, result.CalculatedNetWeightKg);
        Assert.Equal(9m, result.FinalTotalCbm);
    }

    [Fact]
    public async Task ExpenseDefaults_AreCreatedOnceAndUseShipmentCurrency()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-2", Currency = "USD" };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        var service = new ShipmentExpenseService(db);

        await service.EnsureDefaultsAsync(shipment.Id);
        await service.EnsureDefaultsAsync(shipment.Id);

        var rows = db.ShipmentExpenses.Where(x => x.ShipmentId == shipment.Id).ToList();
        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal("USD", row.Currency));
    }

    [Fact]
    public async Task Prepayments_ApplyOldestSameSupplierAndCurrencyFirst()
    {
        await using var db = TestDbFactory.Create();
        db.SupplierPrepayments.AddRange(
            new SupplierPrepayment { No = "ADV-1", SupplierId = 10, Currency = "USD", OriginalAmount = 300m, AvailableAmount = 300m, Status = "available", CreatedAt = new DateTime(2026, 1, 1) },
            new SupplierPrepayment { No = "ADV-2", SupplierId = 10, Currency = "USD", OriginalAmount = 500m, AvailableAmount = 500m, Status = "available", CreatedAt = new DateTime(2026, 2, 1) },
            new SupplierPrepayment { No = "ADV-3", SupplierId = 10, Currency = "RMB", OriginalAmount = 999m, AvailableAmount = 999m, Status = "available", CreatedAt = new DateTime(2026, 3, 1) });
        var payable = new FinanceRecord { No = "AP-1", RecordType = "payable", SupplierId = 10, Currency = "USD", Amount = 600m };
        db.FinanceRecords.Add(payable);
        await db.SaveChangesAsync();

        await new SupplierPrepaymentService(db).ApplyAvailableAsync(payable);

        Assert.Equal(600m, payable.PrepaymentAppliedAmount);
        Assert.Equal(0m, db.SupplierPrepayments.Single(x => x.No == "ADV-1").AvailableAmount);
        Assert.Equal(200m, db.SupplierPrepayments.Single(x => x.No == "ADV-2").AvailableAmount);
        Assert.Equal(999m, db.SupplierPrepayments.Single(x => x.No == "ADV-3").AvailableAmount);
    }

    [Fact]
    public async Task ShipmentSync_IsIdempotentAndCreatesOnePayablePerPositiveExpense()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-3", Currency = "USD", Status = "confirmed" };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        db.ShipmentExpenses.AddRange(
            new ShipmentExpense { ShipmentId = shipment.Id, ExpenseCode = "OCEAN_FREIGHT", ExpenseName = "海运费", NormalizedExpenseName = "海运费", SupplierId = 1, Amount = 1000m, Currency = "USD" },
            new ShipmentExpense { ShipmentId = shipment.Id, ExpenseCode = "WAREHOUSE_FEE", ExpenseName = "仓库费", NormalizedExpenseName = "仓库费", SupplierId = 2, Amount = 300m, Currency = "USD" });
        await db.SaveChangesAsync();

        var expenseService = new ShipmentExpenseService(db);
        var prepaymentService = new SupplierPrepaymentService(db);
        var syncService = new ShipmentFinanceSyncService(db, expenseService, prepaymentService);
        await syncService.SyncAsync(shipment.Id);
        await syncService.SyncAsync(shipment.Id);

        Assert.Equal(2, db.FinanceRecords.Count(x => x.RecordType == "payable" && x.Amount > 0m));
    }

    [Fact]
    public async Task LoweringExpenseBelowPaidAmount_CreatesSupplierPrepayment()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-4", Currency = "USD", Status = "confirmed" };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        var expense = new ShipmentExpense
        {
            ShipmentId = shipment.Id,
            ExpenseCode = "OCEAN_FREIGHT",
            ExpenseName = "海运费",
            NormalizedExpenseName = "海运费",
            SupplierId = 1,
            Amount = 1000m,
            Currency = "USD"
        };
        db.ShipmentExpenses.Add(expense);
        await db.SaveChangesAsync();

        var expenseService = new ShipmentExpenseService(db);
        var prepaymentService = new SupplierPrepaymentService(db);
        var syncService = new ShipmentFinanceSyncService(db, expenseService, prepaymentService);
        await syncService.SyncAsync(shipment.Id);

        var payable = db.FinanceRecords.Single(x => x.ShipmentExpenseId == expense.Id);
        payable.PaidAmount = 800m;
        FinanceBalanceService.RefreshStatus(payable);
        expense.Amount = 700m;
        await db.SaveChangesAsync();

        await syncService.SyncAsync(shipment.Id);

        Assert.Equal(700m, payable.Amount);
        Assert.Equal(100m, payable.OverpaymentTransferredAmount);
        var prepayment = db.SupplierPrepayments.Single(x => x.SourceFinanceRecordId == payable.Id);
        Assert.Equal(100m, prepayment.OriginalAmount);
        Assert.Equal(100m, prepayment.AvailableAmount);
        Assert.Equal(0m, FinanceBalanceService.Outstanding(payable));
    }
}
