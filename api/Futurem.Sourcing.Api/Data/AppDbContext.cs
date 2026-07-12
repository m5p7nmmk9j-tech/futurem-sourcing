using Futurem.Sourcing.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Futurem.Sourcing.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<LogisticsProvider> LogisticsProviders => Set<LogisticsProvider>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OrderProduct> OrderProducts => Set<OrderProduct>();
    public DbSet<OrderProductImage> OrderProductImages => Set<OrderProductImage>();
    public DbSet<CustomerImporterProfile> CustomerImporterProfiles => Set<CustomerImporterProfile>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<SummaryOrder> SummaryOrders => Set<SummaryOrder>();
    public DbSet<SummaryOrderItem> SummaryOrderItems => Set<SummaryOrderItem>();
    public DbSet<DeliveryNotice> DeliveryNotices => Set<DeliveryNotice>();
    public DbSet<DeliveryNoticeLine> DeliveryNoticeLines => Set<DeliveryNoticeLine>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<ReceivingOrder> ReceivingOrders => Set<ReceivingOrder>();
    public DbSet<QcOrder> QcOrders => Set<QcOrder>();
    public DbSet<QcOrderLine> QcOrderLines => Set<QcOrderLine>();
    public DbSet<ContainerLoad> ContainerLoads => Set<ContainerLoad>();
    public DbSet<ContainerLoadSource> ContainerLoadSources => Set<ContainerLoadSource>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentExpense> ShipmentExpenses => Set<ShipmentExpense>();
    public DbSet<FinanceRecord> FinanceRecords => Set<FinanceRecord>();
    public DbSet<FinanceRecordLine> FinanceRecordLines => Set<FinanceRecordLine>();
    public DbSet<FinancialAdjustment> FinancialAdjustments => Set<FinancialAdjustment>();
    public DbSet<SupplierPrepayment> SupplierPrepayments => Set<SupplierPrepayment>();
    public DbSet<SupplierPrepaymentUsage> SupplierPrepaymentUsages => Set<SupplierPrepaymentUsage>();
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
    public DbSet<PrintTemplate> PrintTemplates => Set<PrintTemplate>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();
    public DbSet<MigrationHistory> MigrationHistories => Set<MigrationHistory>();
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();
    public DbSet<RestoreHistory> RestoreHistories => Set<RestoreHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Supplier>().ToTable("suppliers");
        modelBuilder.Entity<LogisticsProvider>().ToTable("logistics_providers");
        modelBuilder.Entity<Market>().ToTable("markets");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<OrderProduct>().ToTable("order_products");
        modelBuilder.Entity<OrderProductImage>().ToTable("order_product_images");
        modelBuilder.Entity<CustomerImporterProfile>().ToTable("customer_importer_profiles");
        modelBuilder.Entity<ProductCategory>().ToTable("product_categories");
        modelBuilder.Entity<Rfq>().ToTable("rfqs");
        modelBuilder.Entity<CustomerOrder>().ToTable("customer_orders");
        modelBuilder.Entity<PurchaseOrder>().ToTable("purchase_orders");
        modelBuilder.Entity<SummaryOrder>().ToTable("summary_orders");
        modelBuilder.Entity<SummaryOrderItem>().ToTable("summary_order_items");
        modelBuilder.Entity<DeliveryNotice>().ToTable("delivery_notices");
        modelBuilder.Entity<DeliveryNoticeLine>().ToTable("delivery_notice_lines");
        modelBuilder.Entity<Warehouse>().ToTable("warehouses");
        modelBuilder.Entity<WarehouseLocation>().ToTable("warehouse_locations");
        modelBuilder.Entity<InventoryLot>().ToTable("inventory_lots");
        modelBuilder.Entity<InventoryTransaction>().ToTable("inventory_transactions");
        modelBuilder.Entity<InventoryReservation>().ToTable("inventory_reservations");
        modelBuilder.Entity<ReceivingOrder>().ToTable("receiving_orders");
        modelBuilder.Entity<QcOrder>().ToTable("qc_orders");
        modelBuilder.Entity<QcOrderLine>().ToTable("qc_order_lines");
        modelBuilder.Entity<ContainerLoad>().ToTable("container_loads");
        modelBuilder.Entity<ContainerLoadSource>().ToTable("container_load_sources");
        modelBuilder.Entity<Shipment>().ToTable("shipments");
        modelBuilder.Entity<ShipmentExpense>().ToTable("shipment_expenses");
        modelBuilder.Entity<FinanceRecord>().ToTable("finance_records");
        modelBuilder.Entity<FinanceRecordLine>().ToTable("finance_record_lines");
        modelBuilder.Entity<FinancialAdjustment>().ToTable("financial_adjustments");
        modelBuilder.Entity<SupplierPrepayment>().ToTable("supplier_prepayments");
        modelBuilder.Entity<SupplierPrepaymentUsage>().ToTable("supplier_prepayment_usages");
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
        modelBuilder.Entity<PrintTemplate>().ToTable("print_templates");
        modelBuilder.Entity<SystemSetting>().ToTable("system_settings");
        modelBuilder.Entity<RefreshToken>().ToTable("refresh_tokens");
        modelBuilder.Entity<UserSession>().ToTable("user_sessions");
        modelBuilder.Entity<LoginLog>().ToTable("login_logs");
        modelBuilder.Entity<SchemaVersion>().ToTable("schema_versions");
        modelBuilder.Entity<MigrationHistory>().ToTable("migration_history");
        modelBuilder.Entity<BackupJob>().ToTable("backup_jobs");
        modelBuilder.Entity<BackupHistory>().ToTable("backup_history");
        modelBuilder.Entity<RestoreHistory>().ToTable("restore_history");

        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Barcode).IsUnique();
        modelBuilder.Entity<LogisticsProvider>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<LogisticsProvider>().HasIndex(x => new { x.Status, x.Name });
        modelBuilder.Entity<OrderProduct>().HasIndex(x => new { x.SourceCustomerOrderId, x.CustomerBarcode }).IsUnique();
        modelBuilder.Entity<OrderProduct>().HasIndex(x => new { x.SourceCustomerOrderId, x.Status });
        modelBuilder.Entity<OrderProductImage>().HasIndex(x => new { x.OrderProductId, x.ImageType });
        modelBuilder.Entity<CustomerImporterProfile>().HasIndex(x => new { x.CustomerId, x.Status, x.IsDefault });
        modelBuilder.Entity<PrintTemplate>().HasIndex(x => new { x.CustomerId, x.TemplateType, x.Status });
        modelBuilder.Entity<SummaryOrderItem>().HasIndex(x => new { x.PurchaseOrderLineId, x.ReservationStatus });
        modelBuilder.Entity<SummaryOrderItem>().HasIndex(x => new { x.SummaryOrderId, x.ReservationStatus });
        modelBuilder.Entity<DeliveryNotice>().HasIndex(x => x.SourceKey).IsUnique();
        modelBuilder.Entity<DeliveryNotice>().HasIndex(x => new { x.SummaryOrderId, x.SupplierId, x.WarehouseId, x.PlannedDeliveryDate });
        modelBuilder.Entity<DeliveryNoticeLine>().HasIndex(x => new { x.DeliveryNoticeId, x.SummaryOrderItemId }).IsUnique();
        modelBuilder.Entity<DeliveryNoticeLine>().HasIndex(x => x.SummaryOrderItemId);
        modelBuilder.Entity<Warehouse>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<WarehouseLocation>().HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        modelBuilder.Entity<InventoryLot>().HasIndex(x => new { x.QcOrderLineId, x.WarehouseId, x.WarehouseLocationId }).IsUnique();
        modelBuilder.Entity<InventoryLot>().HasIndex(x => new { x.CustomerId, x.WarehouseId, x.Status });
        modelBuilder.Entity<InventoryLot>().HasIndex(x => new { x.OrderProductId, x.WarehouseId });
        modelBuilder.Entity<InventoryTransaction>().HasIndex(x => new { x.InventoryLotId, x.CreatedAt });
        modelBuilder.Entity<InventoryReservation>().HasIndex(x => new { x.ContainerLoadId, x.Status });
        modelBuilder.Entity<InventoryReservation>().HasIndex(x => new { x.InventoryLotId, x.Status, x.ExpiresAt });
        modelBuilder.Entity<ContainerLoadSource>().HasIndex(x => new { x.ContainerLoadId, x.InventoryReservationId }).IsUnique();
        modelBuilder.Entity<ContainerLoadSource>().HasIndex(x => new { x.ContainerLoadId, x.SummaryOrderId });
        modelBuilder.Entity<Shipment>().HasIndex(x => x.ContainerLoadId).IsUnique();
        modelBuilder.Entity<ReceivingOrder>().HasIndex(x => x.DeliveryNoticeId);
        modelBuilder.Entity<DocumentLine>().HasIndex(x => x.DeliveryNoticeLineId);
        modelBuilder.Entity<QcOrder>().HasIndex(x => x.ReceivingOrderId).IsUnique();
        modelBuilder.Entity<QcOrderLine>().HasIndex(x => new { x.QcOrderId, x.ReceivingLineId }).IsUnique();
        modelBuilder.Entity<QcOrderLine>().HasIndex(x => x.ReceivingOrderId);
        modelBuilder.Entity<FinanceRecord>().HasIndex(x => x.QcOrderLineId);
        modelBuilder.Entity<FinanceRecord>().HasIndex(x => x.SourceKey);
        modelBuilder.Entity<FinanceRecord>().HasIndex(x => new { x.CounterpartyType, x.LogisticsProviderId, x.Status });
        modelBuilder.Entity<FinanceRecordLine>().HasIndex(x => x.SourceKey).IsUnique();
        modelBuilder.Entity<FinanceRecordLine>().HasIndex(x => new { x.FinanceRecordId, x.CreatedAt });
        modelBuilder.Entity<FinancialAdjustment>().HasIndex(x => x.SourceKey).IsUnique();
        modelBuilder.Entity<FinancialAdjustment>().HasIndex(x => x.FinanceRecordId);
        modelBuilder.Entity<ShipmentExpense>().HasIndex(x => new { x.ShipmentId, x.ExpenseCode }).IsUnique();
        modelBuilder.Entity<ShipmentExpense>().HasIndex(x => new { x.ShipmentId, x.NormalizedExpenseName }).IsUnique();
        modelBuilder.Entity<ShipmentExpense>().HasIndex(x => new { x.LogisticsProviderId, x.ServiceType });
        modelBuilder.Entity<FinanceRecord>().HasIndex(x => x.ShipmentExpenseId).IsUnique();
        modelBuilder.Entity<SupplierPrepayment>().HasIndex(x => new { x.SupplierId, x.Currency, x.Status });
        modelBuilder.Entity<SupplierPrepaymentUsage>().HasIndex(x => new { x.SupplierPrepaymentId, x.FinanceRecordId });
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash);
        modelBuilder.Entity<UserSession>().HasIndex(x => x.SessionId);
        modelBuilder.Entity<SchemaVersion>().HasIndex(x => x.Version);

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
