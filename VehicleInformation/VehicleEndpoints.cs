using Microsoft.EntityFrameworkCore;
using FleetProof.Shared;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace FleetProof.VehicleInformation;

internal static partial class VehicleEndpoints
{
    public static void MapVehicles(this RouteGroupBuilder api)
    {
        var vehicles = api.MapGroup("/vehicles").RequireAuthorization();
        vehicles.MapGet("", async (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
        {
            var accountId = principal.AccountId();
            return await db.Vehicles.AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .OrderBy(x => x.Plate)
                .Select(x => new Resource(x.Id, x.Plate, x.Type, x.Site, x.Owner, x.Risk, x.Monitored))
                .ToListAsync(ct);
        });
        vehicles.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
        {
            var vehicle = await FindOwned(id, principal, db, ct);
            return vehicle is null ? Results.NotFound() : Results.Ok(ToResource(vehicle));
        }).Produces<Resource>(200).Produces(404);
        vehicles.MapPost("", Create).Produces<Resource>(201).Produces(400).Produces(409);
        vehicles.MapPut("/{id:guid}", Update).Produces<Resource>(200).Produces(400).Produces(404).Produces(409);
    }

    private static async Task<IResult> Create(Input input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct)
    {
        var vehicle = new Vehicle { AccountId = principal.AccountId() };
        if (!Apply(input, vehicle))
            return InvalidVehicle();

        db.Vehicles.Add(vehicle);
        return await Save(db, ct)
            ? Results.Created($"/api/v1/vehicles/{vehicle.Id}", ToResource(vehicle))
            : DuplicatePlate();
    }

    private static async Task<IResult> Update(Guid id, Input input, ClaimsPrincipal principal, FleetProofDb db,
        CancellationToken ct)
    {
        var vehicle = await FindOwned(id, principal, db, ct);
        if (vehicle is null)
            return Results.NotFound();
        if (!Apply(input, vehicle))
            return InvalidVehicle();
        return await Save(db, ct) ? Results.Ok(ToResource(vehicle)) : DuplicatePlate();
    }

    private static Task<Vehicle?> FindOwned(Guid id, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct)
    {
        var accountId = principal.AccountId();
        return db.Vehicles.SingleOrDefaultAsync(x => x.Id == id && x.AccountId == accountId, ct);
    }

    private static bool Apply(Input input, Vehicle vehicle)
    {
        var plate = input.Plate?.Trim().ToUpperInvariant() ?? "";
        var type = input.Type?.Trim() ?? "";
        var site = input.Site?.Trim() ?? "";
        var owner = input.Owner?.Trim() ?? "";
        var risk = input.Risk ?? "low";
        if (!PlatePattern().IsMatch(plate) || type.Length is < 1 or > 80 ||
            site.Length > 100 || owner.Length > 120 || risk is not ("low" or "medium" or "high"))
            return false;

        vehicle.Plate = plate;
        vehicle.Type = type;
        vehicle.Site = site;
        vehicle.Owner = owner;
        vehicle.Risk = risk;
        vehicle.Monitored = input.Monitored;
        return true;
    }

    private static async Task<bool> Save(FleetProofDb db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException error) when (error.IsUniqueViolation())
        {
            return false;
        }
    }

    private static Resource ToResource(Vehicle vehicle) =>
        new(vehicle.Id, vehicle.Plate, vehicle.Type, vehicle.Site, vehicle.Owner, vehicle.Risk, vehicle.Monitored);

    private static IResult InvalidVehicle() => Results.BadRequest(new { message = "Invalid vehicle details" });
    private static IResult DuplicatePlate() => Results.Conflict(new { message = "Plate already registered" });

    [GeneratedRegex("^[A-Z0-9]{2,4}-?[A-Z0-9]{2,4}$")]
    private static partial Regex PlatePattern();

    private sealed record Input(string? Plate, string? Type, string? Site, string? Owner, string? Risk, bool Monitored);
    private sealed record Resource(Guid Id, string Plate, string Type, string Site, string Owner, string Risk, bool Monitored);
}
