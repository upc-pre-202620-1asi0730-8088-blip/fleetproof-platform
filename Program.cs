using FleetProof;
using FleetProof.FleetManagement;
using FleetProof.FleetManagement.Infrastructure;
using FleetProof.SubscriptionManagement;
using FleetProof.ReportManagement;
using FleetProof.VehicleMonitoring;
using FleetProof.UserManagement;
using FleetProof.VehicleInformation;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("FleetProof")
    ?? throw new InvalidOperationException("Set ConnectionStrings__FleetProof.");

builder.Services.AddDbContext<FleetProofDb>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ISubscriptionAccessQuery, SubscriptionAccessQuery>();
builder.Services.AddScoped<IFleetEntitlements, SubscriptionEntitlementsAdapter>();
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(options => options.BearerTokenExpiration = TimeSpan.FromHours(1));
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        }));
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(AccountEndpoints.CheckSession);
app.UseAuthorization();
app.MapOpenApi();

var api = app.MapGroup("/api/v1");
api.MapGet("/health", async (FleetProofDb db, CancellationToken cancellation) =>
    await db.Database.CanConnectAsync(cancellation)
        ? Results.Ok(new { status = "ok" })
        : Results.Json(new { status = "unavailable" }, statusCode: 503));
api.MapAccounts();
api.MapVehicles();
api.MapFleetManagement();
api.MapSubscriptions();
api.MapReports();
api.MapCases();
api.MapMonitoring();
app.Run();
