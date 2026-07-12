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
    public async Task ExpenseDefaults_AreCreatedOnceAndUseRmb()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-2", Currency = "USD" };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        var service = new ShipmentExpenseService(db);

        await service.EnsureDefaultsAsync(shipment.Id);
        await service.EnsureDefaultsAsync(shipment.Id);

        var rows = db.ShipmentExpenses.Where(x => x.ShipmentId == shipment.Id).ToList();
        Assert.Equal(6, rows.Count);
        Assert.All(rows, row => Assert.Equal("RMB", row.Currency));
        Assert.Equal("RMB", shipment.Currency);
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
    public async Task ShipmentSync_IsIdempotentAndCreatesOnePayablePerPositiveLogisticsExpense()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-3", Currency = "RMB", Status = "confirmed" };
        var provider1 = new LogisticsProvider { Code = "LP-1", Name = "货代", Status = "active" };
        var provider2 = new LogisticsProvider { Code = "LP-2", Name = "仓库服务商", Status = "active" };
        db.AddRange(shipment, provider1, provider2);
        await db.SaveChangesAsync();
        db.ShipmentExpenses.AddRange(
            new ShipmentExpense { ShipmentId = shipment.Id, ExpenseCode = "OCEAN_FREIGHT", ExpenseName = "海运费", NormalizedExpenseName = "海运费", ServiceType = "ocean_freight", LogisticsProviderId = provider1.Id, ProviderCost = 1000m, CustomerCharge = 1200m, Currency = "RMB" },
            new ShipmentExpense { ShipmentId = shipment.Id, ExpenseCode = "WAREHOUSE_FEE", ExpenseName = "仓储费", NormalizedExpenseName = "仓储费", ServiceType = "warehouse", LogisticsProviderId = provider2.Id, ProviderCost = 300m, CustomerCharge = 400m, Currency = "RMB" });
        await db.SaveChangesAsync();

        var expenseService = new ShipmentExpenseService(db);
        var syncService = new ShipmentFinanceSyncService(db, expenseService, new SupplierPrepaymentService(db));
        await syncService.SyncAsync(shipment.Id);
        await syncService.SyncAsync(shipment.Id);

        Assert.Equal(2, db.FinanceRecords.Count(x => x.RecordType == "payable" && x.Amount > 0m));
        Assert.All(db.FinanceRecords.Where(x => x.RecordType == "payable"), x =>
        {
            Assert.Equal("logistics_provider", x.CounterpartyType);
            Assert.Null(x.SupplierId);
            Assert.NotNull(x.LogisticsProviderId);
        });
        Assert.Equal(1300m, shipment.ExpenseTotal);
        Assert.Equal(1600m, shipment.CustomerChargeTotal);
        Assert.Equal(300m, shipment.LogisticsProfitTotal);
    }

    [Fact]
    public async Task LogisticsPayable_DoesNotCreateProductSupplierPrepayment()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-4", Currency = "RMB", Status = "confirmed" };
        var provider = new LogisticsProvider { Code = "LP-3", Name = "物流公司", Status = "active" };
        db.AddRange(shipment, provider);
        await db.SaveChangesAsync();
        var expense = new ShipmentExpense
        {
            ShipmentId = shipment.Id,
            ExpenseCode = "OCEAN_FREIGHT",
            ExpenseName = "海运费",
            NormalizedExpenseName = "海运费",
            ServiceType = "ocean_freight",
            LogisticsProviderId = provider.Id,
            ProviderCost = 1000m,
            CustomerCharge = 1200m,
            Currency = "RMB"
        };
        db.ShipmentExpenses.Add(expense);
        await db.SaveChangesAsync();

        var syncService = new ShipmentFinanceSyncService(db, new ShipmentExpenseService(db), new SupplierPrepaymentService(db));
        await syncService.SyncAsync(shipment.Id);

        var payable = db.FinanceRecords.Single(x => x.ShipmentExpenseId == expense.Id);
        Assert.Equal(provider.Id, payable.LogisticsProviderId);
        Assert.Null(payable.SupplierId);
        Assert.Empty(db.SupplierPrepayments);
    }
}
