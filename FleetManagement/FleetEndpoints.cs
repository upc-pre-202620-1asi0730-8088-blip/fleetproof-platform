using FleetProof.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace FleetProof.FleetManagement;

internal static class FleetEndpoints
{
    internal static void MapFleetManagement(this RouteGroupBuilder api)
    {
        var fleets = api.MapGroup("/fleets").RequireAuthorization();
        fleets.MapGet("", async (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
        {
            var accountId = principal.AccountId();
            return await db.Fleets.AsNoTracking().Where(x => x.AccountId == accountId)
                .OrderBy(x => x.Name).ThenBy(x => x.Id)
                .Select(x => new FleetResource(x.Id, x.Name)).ToListAsync(ct);
        });
        fleets.MapPost("", CreateFleet).Produces<FleetResource>(201).Produces(400).Produces(403);

        var assignments = api.MapGroup("/assignments").RequireAuthorization();
        assignments.MapGet("", async (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
        {
            var accountId = principal.AccountId();
            return await db.Assignments.AsNoTracking().Where(x => x.AccountId == accountId)
                .OrderBy(x => x.Id)
                .Select(x => new AssignmentResource(x.Id, x.FleetId, x.VehicleId, x.Responsible)).ToListAsync(ct);
        });
        assignments.MapPost("", AssignVehicle).Produces<AssignmentResource>(201)
            .Produces(400).Produces(403).Produces(404).Produces(409);
    }

    private static async Task<IResult> CreateFleet(FleetInput input, ClaimsPrincipal principal,
        FleetProofDb db, IFleetEntitlements entitlements, CancellationToken ct)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 120)
            return Results.BadRequest(new { message = "Invalid fleet name" });

        var accountId = principal.AccountId();
        if (!await entitlements.CanManageFleetAsync(accountId, ct))
            return PlanRequired();
        var fleet = new Fleet { AccountId = accountId, Name = name };
        db.Fleets.Add(fleet);
        await db.SaveChangesAsync(ct);
        return Results.Json(new FleetResource(fleet.Id, fleet.Name), statusCode: 201);
    }

    private static async Task<IResult> AssignVehicle(AssignmentInput input, ClaimsPrincipal principal,
        FleetProofDb db, IFleetEntitlements entitlements, CancellationToken ct)
    {
        var responsible = input.Responsible?.Trim() ?? "";
        if (input.FleetId == Guid.Empty || input.VehicleId == Guid.Empty || responsible.Length is < 1 or > 120)
            return Results.BadRequest(new { message = "Invalid fleet assignment" });

        var accountId = principal.AccountId();
        if (!await entitlements.CanManageFleetAsync(accountId, ct))
            return PlanRequired();
        if (!await db.Fleets.AnyAsync(x => x.Id == input.FleetId && x.AccountId == accountId, ct) ||
            !await db.Vehicles.AnyAsync(x => x.Id == input.VehicleId && x.AccountId == accountId, ct))
            return ReferenceNotFound();

        var assignment = new FleetAssignment
        {
            AccountId = accountId, FleetId = input.FleetId, VehicleId = input.VehicleId, Responsible = responsible
        };
        db.Assignments.Add(assignment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.IsUniqueViolation())
        {
            return Results.Conflict(new { message = "Vehicle is already assigned" });
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return ReferenceNotFound();
        }
        return Results.Json(new AssignmentResource(assignment.Id, assignment.FleetId,
            assignment.VehicleId, assignment.Responsible), statusCode: 201);
    }

    private static IResult PlanRequired() =>
        Results.Json(new { message = "Active plan required" }, statusCode: 403);

    private static IResult ReferenceNotFound() =>
        Results.NotFound(new { message = "Fleet or vehicle not found" });

    private sealed record FleetInput(string? Name);
    private sealed record FleetResource(Guid Id, string Name);
    private sealed record AssignmentInput(Guid FleetId, Guid VehicleId, string? Responsible);
    private sealed record AssignmentResource(Guid Id, Guid FleetId, Guid VehicleId, string Responsible);
}
