using System.Text.Json.Serialization;
using Cartex.Api.Hubs;
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
    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "cartex-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

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
builder.Services.AddScoped<ICurrentCustomer, CurrentCustomer>();
builder.Services.AddScoped<IPagingMetadataWriter, HttpPagingMetadataWriter>();
builder.Services.AddSingleton<ICartNotifier, SignalRCartNotifier>();
builder.Services.AddSingleton<IPrintJobNotifier, SignalRPrintJobNotifier>();
builder.Services.AddHostedService<TelegramUpdatePoller>();
builder.Services.AddHostedService<PrintJobRecoveryService>();

builder.Services.AddSignalR();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials().WithExposedHeaders("X-Paging");
    });
});

var trustProxyHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (trustProxyHeaders)
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

var authPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 10);
var publicPerMinute = builder.Configuration.GetValue("RateLimiting:PublicPerMinute", 60);
var authenticatedPerMinute = builder.Configuration.GetValue("RateLimiting:AuthenticatedPerMinute", 600);
var printingPerMinute = builder.Configuration.GetValue("RateLimiting:PrintingPerMinute", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));

    // Staff terminals read receipts through the same public endpoint, so an anonymous
    // per-IP bucket would throttle a busy till. Signed-in callers get their own bucket.
    options.AddPolicy("public", context =>
        context.User.FindFirst("userId")?.Value is { Length: > 0 } userId
            ? System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                $"user:{userId}",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = authenticatedPerMinute, Window = TimeSpan.FromMinutes(1) })
            : System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = publicPerMinute, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("printing", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            $"{context.User.FindFirst("userId")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{context.Request.Headers["X-Device-Id"]}",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = printingPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});

builder.Host.UseWindowsService();
// localhost already covers 127.0.0.1 and ::1 — listing both binds the same socket twice.
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5015");

var app = builder.Build();

var developerPassword = builder.Configuration["Seed:DeveloperPassword"];
var adminPassword = builder.Configuration["Seed:AdminPassword"];
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(developerPassword))
    throw new InvalidOperationException("Seed:DeveloperPassword production muhitida majburiy.");
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(adminPassword))
    throw new InvalidOperationException("Seed:AdminPassword production muhitida majburiy.");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Migrations are squashed into a single InitialMigration before release, so a database
    // stamped with a migration this build no longer contains can never be migrated forward.
    var known = db.Database.GetMigrations().ToHashSet();
    var orphaned = (await db.Database.GetAppliedMigrationsAsync()).Where(x => !known.Contains(x)).ToList();
    if (orphaned.Count > 0)
        throw new InvalidOperationException(
            $"Bazada bu buildda mavjud bo'lmagan migratsiya(lar) qo'llangan: {string.Join(", ", orphaned)}. " +
            "Migratsiyalar birlashtirilgan bo'lsa, dev bazani qayta yarating " +
            "(DROP DATABASE cartex_db; CREATE DATABASE cartex_db;). " +
            "Production'da bu eski build deploy qilinganini bildiradi.");

    await db.Database.MigrateAsync();

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DatabaseSeeder.SeedAsync(db, hasher.Hash, developerPassword, adminPassword, seedSeller: app.Environment.IsDevelopment());
    await DatabaseSeeder.SyncPermissionsAsync(db);
    await DatabaseSeeder.SyncUnitsAsync(db);
    await DatabaseSeeder.SyncFeaturesAsync(db);
    await DatabaseSeeder.SyncCurrenciesAsync(db);
    await DatabaseSeeder.SyncStorageDefaultAsync(db);
    await DatabaseSeeder.EnsureDeveloperPasswordAsync(db, hasher.Verify, hasher.Hash, developerPassword);
    await DatabaseSeeder.EnsureAdminPasswordAsync(db, hasher.Verify, hasher.Hash, adminPassword);

    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:Demo"))
        await DemoDataSeeder.SeedAsync(db);
    else if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:Catalog"))
        await DemoDataSeeder.SeedCatalogAsync(db);
}

if (trustProxyHeaders)
    app.UseForwardedHeaders();

app.UseResponseCompression();

var hasWebUi = File.Exists(Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html"));
if (hasWebUi)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseSerilogRequestLogging(options => options.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
    : ctx.Request.Path.StartsWithSegments("/health") || ctx.Request.Path.StartsWithSegments("/api/printing/nodes/heartbeat") ? Serilog.Events.LogEventLevel.Verbose
    : Serilog.Events.LogEventLevel.Information);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("Default");

app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsDevelopment())
    app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Cartex API");
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.MapControllers();

app.MapHub<OrderingHub>("/hubs/ordering");
app.MapHub<PrintingHub>("/hubs/printing");

app.MapGet("/health", () => Results.Ok()).AllowAnonymous();

if (hasWebUi)
{
    app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Run();

public partial class Program;
