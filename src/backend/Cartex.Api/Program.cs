using System.Text.Json.Serialization;
using Cartex.Api.Middleware;
using Cartex.Application;
using Cartex.Auth;
using Cartex.Auth.Services;
using Cartex.Persistence;
using Cartex.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddPersistence(connectionString);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddOpenApi();

builder.WebHost.UseUrls("http://localhost:5015");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DatabaseSeeder.SeedAsync(db, hasher.Hash);
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("AllowAll");

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

app.Run();
