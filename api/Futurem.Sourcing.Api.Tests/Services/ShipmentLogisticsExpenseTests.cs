using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;

namespace Futurem.Sourcing.Api.Tests.Services;

public class ShipmentLogisticsExpenseTests
{
    [Fact]
    public async Task Validate_AllowsDifferentLogisticsProvidersAndCalculatesProfit()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-L1", Status = "draft", Currency = "RMB" };
        var ocean = NewProvider("LP-OCEAN", "海运货代", "ocean_freight");
        var customs = NewProvider("LP-CUSTOMS", "报关行", "customs");
        var truck = NewProvider("LP-TRUCK", "拖车公司", "trucking");
        db.AddRange(shipment, ocean, customs, truck);
        await db.SaveChangesAsync();
        var service = new ShipmentExpenseService(db);

        var expenses = new[]
        {
            NewExpense(shipment.Id, ocean.Id, "OCEAN_FREIGHT", 20000m, 23000m),
            NewExpense(shipment.Id, customs.Id, "CUSTOMS", 1000m, 1500m),
            NewExpense(shipment.Id, truck.Id, "TRUCKING", 2000m, 2500m)
        };

        foreach (var expense in expenses)
            await service.ValidateAsync(shipment, expense);

        Assert.Equal(3000m, expenses[0].ProfitAmount);
        Assert.Equal(500m, expenses[1].ProfitAmount);
        Assert.Equal(500m, expenses[2].ProfitAmount);
        Assert.Equal(20000m, expenses[0].Amount);
        Assert.All(expenses, x => Assert.Equal("RMB", x.Currency));
        Assert.Equal(3, expenses.Select(x => x.LogisticsProviderId).Distinct().Count());
    }

    [Fact]
    public async Task Validate_DoesNotAcceptProductSupplierAsLogisticsProvider()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-L2", Status = "draft", Currency = "RMB" };
        var productSupplier = new Supplier { Code = "PRODUCT-SUP", Name = "商品供应商" };
        db.AddRange(shipment, productSupplier);
        await db.SaveChangesAsync();
        var service = new ShipmentExpenseService(db);
        var expense = NewExpense(shipment.Id, productSupplier.Id, "OCEAN_FREIGHT", 100m, 120m);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ValidateAsync(shipment, expense));

        Assert.Equal("LOGISTICS_PROVIDER_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Validate_RequiresProviderWhenEitherCostOrCustomerChargeIsPositive()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-L3", Status = "draft", Currency = "RMB" };
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        var service = new ShipmentExpenseService(db);
        var expense = NewExpense(shipment.Id, null, "CUSTOMS", 0m, 500m);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ValidateAsync(shipment, expense));

        Assert.Equal("LOGISTICS_PROVIDER_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task Validate_RejectsNegativeCostOrCustomerCharge()
    {
        await using var db = TestDbFactory.Create();
        var shipment = new Shipment { No = "SHP-L4", Status = "draft", Currency = "RMB" };
        var provider = NewProvider("LP-NEG", "服务商", "other_service");
        db.AddRange(shipment, provider);
        await db.SaveChangesAsync();
        var service = new ShipmentExpenseService(db);

        var costEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ValidateAsync(shipment, NewExpense(shipment.Id, provider.Id, "OTHER", -1m, 0m)));
        var chargeEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ValidateAsync(shipment, NewExpense(shipment.Id, provider.Id, "OTHER", 0m, -1m)));

        Assert.Equal("LOGISTICS_AMOUNT_NEGATIVE", costEx.Code);
        Assert.Equal("LOGISTICS_AMOUNT_NEGATIVE", chargeEx.Code);
    }

    private static LogisticsProvider NewProvider(string code, string name, string serviceType)
        => new()
        {
            Code = code,
            Name = name,
            ServiceTypesJson = $"[\"{serviceType}\"]",
            Status = "active"
        };

    private static ShipmentExpense NewExpense(
        long shipmentId,
        long? logisticsProviderId,
        string serviceType,
        decimal providerCost,
        decimal customerCharge)
        => new()
        {
            ShipmentId = shipmentId,
            ExpenseCode = serviceType,
            ExpenseName = serviceType,
            ServiceType = serviceType,
            LogisticsProviderId = logisticsProviderId,
            ProviderCost = providerCost,
            CustomerCharge = customerCharge,
            IsCustom = serviceType == "OTHER"
        };
}
