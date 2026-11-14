using System.Security.Claims;
using FleetProof.Shared;
using FleetProof.SubscriptionManagement;
using FleetProof.VehicleInformation;
using Microsoft.EntityFrameworkCore;

namespace FleetProof.VehicleMonitoring;

public sealed class VehicleWatch : IOwnedDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public Guid VehicleId { get; init; }
    public bool Active { get; set; }
    public string Data { get; set; } = "{}";
}

public sealed class MonitoringAlert : IOwnedDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public string Data { get; set; } = "{}";
}

internal static class MonitoringEndpoints
{
    internal static void ConfigureMonitoring(this ModelBuilder builder)
    {
        builder.ConfigureDocument<VehicleWatch>("vehicle_monitorings");
        builder.ConfigureDocument<MonitoringAlert>("monitoring_alerts");
        var entity = builder.Entity<VehicleWatch>();
        entity.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        entity.Property(x => x.Active).HasColumnName("active");
        entity.HasIndex(x => new { x.AccountId, x.VehicleId }).IsUnique().HasFilter("active");
        entity.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.AccountId, x.VehicleId })
            .HasPrincipalKey(x => new { x.AccountId, x.Id });
    }

    internal static void MapMonitoring(this RouteGroupBuilder api)
    {
        var watches = api.MapGroup("/monitorings").RequireAuthorization();
        watches.MapGet("", (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            db.List<VehicleWatch, Watch>(principal.AccountId(), ct));
        watches.MapPost("", (Watch input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(null, input, principal, db, ct)).Produces<Watch>(201);
        watches.MapPut("/{id:guid}", (Guid id, Watch input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(id, input, principal, db, ct)).Produces<Watch>();

        var alerts = api.MapGroup("/alerts").RequireAuthorization();
        alerts.MapGet("", (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            db.List<MonitoringAlert, Alert>(principal.AccountId(), ct));
        alerts.MapPost("", RecordAlert).Produces<Alert>(201);
    }

    private static async Task<IResult> Save(Guid? id, Watch input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct)
    {
        if (input.IntervalMinutes is < 1 or > 525600 || input.Status is not ("active" or "inactive") ||
            input.VehicleId == Guid.Empty)
            return Results.BadRequest(new { message = "Invalid monitoring schedule" });

        var accountId = principal.AccountId();
        if (input.Status == "active" && !await db.HasPlan(accountId, "monitoring", ct))
            return SubscriptionEndpoints.PlanRequired();
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(
            v => v.AccountId == accountId && v.Id == input.VehicleId, ct);
        if (vehicle is null) return Results.NotFound();
        var row = id is Guid existing
            ? await db.Owned<VehicleWatch>(accountId).SingleOrDefaultAsync(x => x.Id == existing, ct)
            : new VehicleWatch { AccountId = accountId, VehicleId = vehicle.Id };
        if (row is null) return Results.NotFound();
        if (row.VehicleId != vehicle.Id) return Results.BadRequest(new { message = "Vehicle cannot change" });

        var previous = id is null ? null : row.Read<Watch>();
        var now = DateTimeOffset.UtcNow;
        var checkedNow = previous is not null && !string.IsNullOrEmpty(input.LastCheckedAt) &&
            input.LastCheckedAt != previous.LastCheckedAt && input.Status == "active";
        var saved = input with
        {
            Id = row.Id, Snapshot = previous is null || checkedNow ? Capture(vehicle) : previous.Snapshot,
            LastCheckedAt = checkedNow ? input.LastCheckedAt : previous?.LastCheckedAt ?? "",
            NextCheckAt = previous is null || checkedNow || previous.Status != input.Status
                ? now.AddMinutes(input.IntervalMinutes).ToString("O") : previous.NextCheckAt
        };
        if (checkedNow && !DateTimeOffset.TryParse(input.LastCheckedAt, out _))
            return Results.BadRequest(new { message = "Invalid check timestamp" });
        row.Active = input.Status == "active";
        row.Write(saved);
        if (id is null) db.Add(row);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        { return Results.Conflict(new { message = "Vehicle already monitored" }); }
        return Results.Json(saved, statusCode: id is null ? 201 : 200);
    }

    private static async Task<IResult> RecordAlert(Alert input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct)
    {
        var accountId = principal.AccountId();
        if (!await db.HasPlan(accountId, "monitoring", ct)) return SubscriptionEndpoints.PlanRequired();
        var watch = await db.Owned<VehicleWatch>(accountId).AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == input.MonitoringId && x.VehicleId == input.VehicleId && x.Active, ct);
        if (watch is null) return Results.NotFound();
        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == watch.VehicleId && v.AccountId == accountId, ct);
        var before = watch.Read<Watch>().Snapshot!;
        var after = Capture(vehicle);
        var changes = after.Where(x => before.GetValueOrDefault(x.Key) != x.Value)
            .Select(x => new Change(x.Key, before.GetValueOrDefault(x.Key), x.Value)).ToArray();
        if (changes.Length == 0) return Results.Conflict(new { message = "No vehicle changes detected" });
        var row = new MonitoringAlert { AccountId = accountId };
        var saved = new Alert(row.Id, watch.Id, watch.VehicleId, changes, DateTimeOffset.UtcNow);
        row.Write(saved);
        db.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Json(saved, statusCode: 201);
    }

    private static Dictionary<string, string> Capture(Vehicle v) => new()
    {
        ["plate"] = v.Plate, ["risk"] = v.Risk, ["site"] = v.Site, ["owner"] = v.Owner, ["type"] = v.Type
    };

    private sealed record Watch(Guid Id, Guid VehicleId, int IntervalMinutes, string? Status,
        Dictionary<string, string>? Snapshot, string? LastCheckedAt, string? NextCheckAt);
    private sealed record Alert(Guid Id, Guid MonitoringId, Guid VehicleId, Change[]? Changes, DateTimeOffset? CreatedAt);
    private sealed record Change(string Field, string? Before, string After);
}
