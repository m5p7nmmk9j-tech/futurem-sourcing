using Futurem.Sourcing.Api.Controllers;
using Futurem.Sourcing.Api.Entities;
using Futurem.Sourcing.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Futurem.Sourcing.Api.Tests.Controllers;

public class CustomerImporterProfilesControllerTests
{
    [Fact]
    public async Task Update_UnsettingOnlyDefault_PromotesAnotherActiveProfile()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "C001", Name = "客户一" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var first = NewProfile(customer.Id, "进口商一", isDefault: true);
        var second = NewProfile(customer.Id, "进口商二", isDefault: false);
        db.CustomerImporterProfiles.AddRange(first, second);
        await db.SaveChangesAsync();

        var controller = new CustomerImporterProfilesController(db);
        var input = NewProfile(customer.Id, first.Name, isDefault: false);
        input.CompanyName = first.CompanyName;
        input.Address = first.Address;

        await controller.Update(first.Id, input);

        Assert.False(first.IsDefault);
        Assert.True(second.IsDefault);
        Assert.Single(await db.CustomerImporterProfiles.Where(x => x.CustomerId == customer.Id && x.IsDefault).ToListAsync());
    }

    [Fact]
    public async Task Update_UnsettingOnlyProfile_KeepsItDefault()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "C001", Name = "客户一" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var profile = NewProfile(customer.Id, "唯一进口商", isDefault: true);
        db.CustomerImporterProfiles.Add(profile);
        await db.SaveChangesAsync();

        var controller = new CustomerImporterProfilesController(db);
        var input = NewProfile(customer.Id, profile.Name, isDefault: false);
        input.CompanyName = profile.CompanyName;
        input.Address = profile.Address;

        await controller.Update(profile.Id, input);

        Assert.True(profile.IsDefault);
    }

    [Fact]
    public async Task Delete_RejectsProfileReferencedByDraftOrder()
    {
        await using var db = TestDbFactory.Create();
        var customer = new Customer { Code = "C001", Name = "客户一" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var profile = NewProfile(customer.Id, "进口商一", isDefault: true);
        db.CustomerImporterProfiles.Add(profile);
        await db.SaveChangesAsync();
        db.CustomerOrders.Add(new CustomerOrder
        {
            No = "CO001",
            CustomerId = customer.Id,
            ImporterProfileId = profile.Id,
            Status = "draft",
            Currency = "RMB"
        });
        await db.SaveChangesAsync();

        var controller = new CustomerImporterProfilesController(db);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Delete(profile.Id));

        Assert.Equal("IMPORTER_IN_USE", ex.Code);
        Assert.False(profile.IsDeleted);
    }

    private static CustomerImporterProfile NewProfile(long customerId, string name, bool isDefault) => new()
    {
        CustomerId = customerId,
        Name = name,
        CompanyName = $"{name}公司",
        Address = "Ciudad de México",
        IsDefault = isDefault,
        Status = "active"
    };
}
