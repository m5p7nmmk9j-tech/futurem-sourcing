using System.Text;
using Futurem.Sourcing.Api.Data;
using Futurem.Sourcing.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FUTUREM Enterprise API", Version = "v2.0.0" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Input JWT Bearer token"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DatabaseUpgradeService>();
builder.Services.AddScoped<AuditSchemaUpgradeService>();
builder.Services.AddScoped<OrderProductSchemaUpgradeService>();
builder.Services.AddScoped<OrderProductIndexUpgradeService>();
builder.Services.AddScoped<SummaryReservationSchemaUpgradeService>();
builder.Services.AddScoped<DeliveryNoticeSchemaUpgradeService>();
builder.Services.AddScoped<QcConfirmationSchemaUpgradeService>();
builder.Services.AddScoped<InventorySchemaUpgradeService>();
builder.Services.AddScoped<ContainerReservationSchemaUpgradeService>();
builder.Services.AddScoped<ContainerConfirmationSchemaUpgradeService>();
builder.Services.AddScoped<LogisticsProviderSchemaUpgradeService>();
builder.Services.AddScoped<DeliveryNoticeService>();
builder.Services.AddScoped<QcConfirmationService>();
builder.Services.AddScoped<SummaryReservationService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ContainerReservationService>();
builder.Services.AddScoped<ContainerConfirmationService>();
builder.Services.AddScoped<FinanceDocumentService>();
builder.Services.AddScoped<ShipmentMeasurementService>();
builder.Services.AddScoped<ShipmentExpenseService>();
builder.Services.AddScoped<ShipmentDepartureService>();
builder.Services.AddScoped<SupplierPrepaymentService>();
builder.Services.AddScoped<ShipmentFinanceSyncService>();
builder.Services.AddScoped<AuditTrailService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ContainerReservationExpiryWorker>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "server=127.0.0.1;port=3306;database=futurem_sourcing;user=root;password=;CharSet=utf8mb4;";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? new string('X', 40);
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FUTUREM";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FUTUREM_WEB";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<AuditSchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<OrderProductSchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<OrderProductIndexUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<SummaryReservationSchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<DeliveryNoticeSchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<QcConfirmationSchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<InventorySchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<ContainerReservationSchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<ContainerConfirmationSchemaUpgradeService>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<LogisticsProviderSchemaUpgradeService>().UpgradeAsync();
}

app.UseMiddleware<BusinessRuleExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("DevCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = "FUTUREM Enterprise Sourcing API",
    status = "running",
    version = "2.0.0"
}));

app.Run();
