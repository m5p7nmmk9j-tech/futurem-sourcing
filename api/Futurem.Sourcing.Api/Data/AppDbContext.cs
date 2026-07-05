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
    public DbSet<ContainerLoad> ContainerLoads => Set<ContainerLoad>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<FinanceRecord> FinanceRecords => Set<FinanceRecord>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<DocumentLine> DocumentLines => Set<DocumentLine>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
        modelBuilder.Entity<ContainerLoad>().ToTable("container_loads");
        modelBuilder.Entity<Shipment>().ToTable("shipments");
        modelBuilder.Entity<FinanceRecord>().ToTable("finance_records");
        modelBuilder.Entity<BankAccount>().ToTable("bank_accounts");
        modelBuilder.Entity<Payment>().ToTable("payments");
        modelBuilder.Entity<DocumentLine>().ToTable("document_lines");
        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<ApprovalRequest>().ToTable("approval_requests");
        modelBuilder.Entity<ApprovalStep>().ToTable("approval_steps");
        modelBuilder.Entity<Role>().ToTable("roles");
        modelBuilder.Entity<Permission>().ToTable("permissions");
        modelBuilder.Entity<UserAccount>().ToTable("user_accounts");
        modelBuilder.Entity<RolePermission>().ToTable("role_permissions");
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");
        ApplySnakeCaseColumnNames(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (typeof(BaseEntity).IsAssignableFrom(clr)) modelBuilder.Entity(clr).HasQueryFilter(BuildIsNotDeletedFilter(clr));
        }
    }

    private static System.Linq.Expressions.LambdaExpression BuildIsNotDeletedFilter(Type type)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(type, "x");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var body = System.Linq.Expressions.Expression.Equal(property, System.Linq.Expressions.Expression.Constant(false));
        return System.Linq.Expressions.Expression.Lambda(body, parameter);
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
