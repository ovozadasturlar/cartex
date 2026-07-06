using System.Text.Json.Serialization;
using Cartex.Api.Middleware;
using Cartex.Api.Services;
using Cartex.Application;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common;
using Cartex.Auth;
using Cartex.Auth.Services;
using Cartex.Infrastructure;
using Cartex.Infrastructure.Web;
using Cartex.Persistence;
using Cartex.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/cartex-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddPersistence(connectionString);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, Cartex.Api.Authorization.FeatureAwareAuthorizationResultHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IPagingMetadataWriter, HttpPagingMetadataWriter>();
builder.Services.AddHostedService<TelegramUpdatePoller>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.AddOpenApi();

builder.Host.UseWindowsService();
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5015");

var app = builder.Build();

var developerPassword = builder.Configuration["Seed:DeveloperPassword"];
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(developerPassword))
    throw new InvalidOperationException("Seed:DeveloperPassword production muhitida majburiy.");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DatabaseSeeder.SeedAsync(db, hasher.Hash, developerPassword);
    await DatabaseSeeder.SyncPermissionsAsync(db);
    await DatabaseSeeder.SyncUnitsAsync(db);
    await DatabaseSeeder.SyncFeaturesAsync(db);
    await DatabaseSeeder.EnsureDeveloperPasswordAsync(db, hasher.Verify, hasher.Hash, developerPassword);

    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:Demo"))
        await DemoDataSeeder.SeedAsync(db);
}

app.UseSerilogRequestLogging();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("Default");

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Cartex API");
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapControllers();

app.MapGet("/health", () => Results.Ok()).AllowAnonymous();

app.Run();

public partial class Program;
