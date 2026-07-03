using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Futurem.Sourcing.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<SummaryOrder> SummaryOrders => Set<SummaryOrder>();
    public DbSet<ReceivingOrder> ReceivingOrders => Set<ReceivingOrder>();
    public DbSet<QcOrder> QcOrders => Set<QcOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Supplier>().ToTable("suppliers");
        modelBuilder.Entity<Market>().ToTable("markets");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<ProductCategory>().ToTable("product_categories");
        modelBuilder.Entity<Rfq>().ToTable("rfqs");
        modelBuilder.Entity<CustomerOrder>().ToTable("customer_orders");
        modelBuilder.Entity<PurchaseOrder>().ToTable("purchase_orders");
        modelBuilder.Entity<SummaryOrder>().ToTable("summary_orders");
        modelBuilder.Entity<ReceivingOrder>().ToTable("receiving_orders");
        modelBuilder.Entity<QcOrder>().ToTable("qc_orders");
        ApplySnakeCaseColumnNames(modelBuilder);
        modelBuilder.Entity<Customer>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Supplier>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Market>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ProductCategory>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Rfq>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<CustomerOrder>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<SummaryOrder>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ReceivingOrder>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QcOrder>().HasQueryFilter(x => !x.IsDeleted);
    }

    private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties()) property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]))) chars.Add('_');
                chars.Add(char.ToLowerInvariant(c));
            }
            else chars.Add(c);
        }
        return new string(chars.ToArray());
    }
}
