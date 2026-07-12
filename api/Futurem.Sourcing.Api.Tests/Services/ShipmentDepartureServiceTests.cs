using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Services;

public class ShipmentDepartureServiceTests
{
    [Fact]
    public async Task ConfirmDeparture_AppendsCustomerChargesToExistingContainerReceivable()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = CreateService(db);

        var result = await service.ConfirmDepartureAsync(fixture.Shipment.Id, 7);

        Assert.Equal("shipped", result.Shipment.Status);
        Assert.Equal(fixture.GoodsReceivable.Id, result.CustomerReceivable.Id);
        Assert.Equal(105000m, result.CustomerReceivable.Amount);
        Assert.Equal(60000m, result.CustomerReceivable.PaidAmount);
        Assert.Equal(45000m, FinanceBalanceService.Outstanding(result.CustomerReceivable));
        var customerLines = await db.FinanceRecordLines
            .Where(x => x.FinanceRecordId == fixture.GoodsReceivable.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        Assert.Equal(3, customerLines.Count);
        Assert.Contains(customerLines, x => x.SourceKey == $"shipment:{fixture.Shipment.Id}:expense:{fixture.OceanExpense.Id}:customer" && x.Amount == 4000m);
        Assert.Contains(customerLines, x => x.SourceKey == $"shipment:{fixture.Shipment.Id}:expense:{fixture.CustomsExpense.Id}:customer" && x.Amount == 1000m);
    }

    [Fact]
    public async Task ConfirmDeparture_CreatesUniqueProviderPayablesAndIsIdempotent()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        var service = CreateService(db);

        var first = await service.ConfirmDepartureAsync(fixture.Shipment.Id, 7);
        var second = await service.ConfirmDepartureAsync(fixture.Shipment.Id, 7);

        Assert.Equal(first.Shipment.Id, second.Shipment.Id);
        var payables = await db.FinanceRecords
            .Where(x => x.RecordType == "payable" && x.CounterpartyType == "logistics_provider")
            .OrderBy(x => x.SourceKey)
            .ToListAsync();
        Assert.Equal(2, payables.Count);
        Assert.Contains(payables, x =>
            x.SourceKey == $"shipment:{fixture.Shipment.Id}:expense:{fixture.OceanExpense.Id}:provider" &&
            x.LogisticsProviderId == fixture.Provider1.Id && x.Amount == 3000m);
        Assert.Contains(payables, x =>
            x.SourceKey == $"shipment:{fixture.Shipment.Id}:expense:{fixture.CustomsExpense.Id}:provider" &&
            x.LogisticsProviderId == fixture.Provider2.Id && x.Amount == 800m);
        Assert.Equal(2, await db.FinanceRecordLines.CountAsync(x => x.LineType == "logistics_provider_cost"));
        Assert.Equal(2, await db.FinanceRecordLines.CountAsync(x => x.LineType == "logistics_customer_charge"));
    }

    [Fact]
    public async Task ConfirmDeparture_RequiresConfirmedShipmentAndContainerRelation()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        fixture.Shipment.Status = "draft";
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var statusEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDepartureAsync(fixture.Shipment.Id, 7));
        Assert.Equal("SHIPMENT_NOT_CONFIRMED", statusEx.Code);

        fixture.Shipment.Status = "confirmed";
        fixture.Shipment.ContainerLoadId = null;
        await db.SaveChangesAsync();
        var sourceEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDepartureAsync(fixture.Shipment.Id, 7));
        Assert.Equal("SHIPMENT_CONTAINER_REQUIRED", sourceEx.Code);
    }

    [Fact]
    public async Task ConfirmDeparture_RejectsMissingContainerAndLogisticsData()
    {
        await using var db = TestDbFactory.Create();
        var fixture = await CreateFixtureAsync(db);
        fixture.Shipment.ContainerNo = null;
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var containerEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDepartureAsync(fixture.Shipment.Id, 7));
        Assert.Equal("SHIPMENT_CONTAINER_NO_REQUIRED", containerEx.Code);

        fixture.Shipment.ContainerNo = "CONT-DEP";
        fixture.OceanExpense.LogisticsProviderId = null;
        await db.SaveChangesAsync();
        var providerEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ConfirmDepartureAsync(fixture.Shipment.Id, 7));
        Assert.Equal("LOGISTICS_PROVIDER_REQUIRED", providerEx.Code);
    }

    private static ShipmentDepartureService CreateService(Futurem.Sourcing.Api.Data.AppDbContext db)
    {
        var expenseService = new ShipmentExpenseService(db);
        return new ShipmentDepartureService(
            db,
            expenseService,
            new FinanceDocumentService(db),
            new AuditTrailService(db));
    }

    private static async Task<Fixture> CreateFixtureAsync(Futurem.Sourcing.Api.Data.AppDbContext db)
    {
        var customer = new Customer { Code = "DEP-C", Name = "发运客户", Currency = "RMB" };
        var warehouse = new Warehouse { Code = "DEP-W", Name = "发运仓库", Status = "active" };
        var provider1 = new LogisticsProvider { Code = "DEP-L1", Name = "货代一", ServiceTypesJson = "[\"ocean_freight\"]", Status = "active" };
        var provider2 = new LogisticsProvider { Code = "DEP-L2", Name = "报关行二", ServiceTypesJson = "[\"customs\"]", Status = "active" };
        db.AddRange(customer, warehouse, provider1, provider2);
        await db.SaveChangesAsync();

        var container = new ContainerLoad
        {
            No = "CL-DEP",
            CustomerId = customer.Id,
            WarehouseId = warehouse.Id,
            ContainerType = "40HQ",
            ContainerNo = "CONT-DEP",
            SealNo = "SEAL-DEP",
            Status = "shipment_created"
        };
        db.ContainerLoads.Add(container);
        await db.SaveChangesAsync();

        var shipment = new Shipment
        {
            No = "SHP-DEP",
            ContainerLoadId = container.Id,
            CustomerId = customer.Id,
            WarehouseId = warehouse.Id,
            ContainerType = container.ContainerType,
            ContainerNo = container.ContainerNo,
            SealNo = container.SealNo,
            ShipmentMode = "SEA",
            DeparturePort = "NINGBO",
            DestinationPort = "MANZANILLO",
            Etd = new DateTime(2026, 7, 13),
            Status = "confirmed",
            Currency = "RMB"
        };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        var goodsReceivable = new FinanceRecord
        {
            No = "AR-GOODS",
            RecordType = "receivable",
            TargetType = "CONTAINER_LOAD",
            TargetId = container.Id,
            CustomerId = customer.Id,
            CounterpartyType = "customer",
            Currency = "RMB",
            SourceKey = $"container:{container.Id}:goods",
            Amount = 100000m,
            PaidAmount = 60000m,
            Status = "partial",
            RecordDate = DateTime.Today
        };
        db.FinanceRecords.Add(goodsReceivable);
        await db.SaveChangesAsync();
        db.FinanceRecordLines.Add(new FinanceRecordLine
        {
            FinanceRecordId = goodsReceivable.Id,
            SourceKey = $"container:{container.Id}:source:1:goods",
            LineType = "goods",
            SourceType = "CONTAINER_LOAD_SOURCE",
            SourceId = 1,
            Quantity = 1000m,
            UnitPrice = 100m,
            Amount = 100000m,
            PaidAmount = 60000m,
            Status = "partial"
        });

        var oceanExpense = new ShipmentExpense
        {
            ShipmentId = shipment.Id,
            ExpenseCode = "OCEAN_FREIGHT",
            ExpenseName = "海运费",
            NormalizedExpenseName = "海运费",
            ServiceType = "ocean_freight",
            LogisticsProviderId = provider1.Id,
            ProviderCost = 3000m,
            CustomerCharge = 4000m,
            ProfitAmount = 1000m,
            Amount = 3000m,
            Currency = "RMB"
        };
        var customsExpense = new ShipmentExpense
        {
            ShipmentId = shipment.Id,
            ExpenseCode = "CUSTOMS",
            ExpenseName = "报关费",
            NormalizedExpenseName = "报关费",
            ServiceType = "customs",
            LogisticsProviderId = provider2.Id,
            ProviderCost = 800m,
            CustomerCharge = 1000m,
            ProfitAmount = 200m,
            Amount = 800m,
            Currency = "RMB"
        };
        db.AddRange(oceanExpense, customsExpense);
        await db.SaveChangesAsync();
        return new Fixture(customer, warehouse, container, shipment, provider1, provider2, oceanExpense, customsExpense, goodsReceivable);
    }

    private sealed record Fixture(
        Customer Customer,
        Warehouse Warehouse,
        ContainerLoad Container,
        Shipment Shipment,
        LogisticsProvider Provider1,
        LogisticsProvider Provider2,
        ShipmentExpense OceanExpense,
        ShipmentExpense CustomsExpense,
        FinanceRecord GoodsReceivable);
}
