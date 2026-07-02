using Futurem.Sourcing.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "server=127.0.0.1;port=3306;database=futurem_sourcing;user=root;password=;CharSet=utf8mb4;";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("DevCors");
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = "FUTUREM Sourcing API",
    status = "running",
    version = "0.1.0-alpha"
}));

app.Run();
