using System.Security.Claims;
using FleetProof.Shared;
using Microsoft.EntityFrameworkCore;

namespace FleetProof.ReportManagement;

public sealed class VehicleReport : IOwnedDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public string Data { get; set; } = "{}";
}

internal static class ReportEndpoints
{
    internal static void MapReports(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/reports").RequireAuthorization();
        routes.MapGet("", (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            db.List<VehicleReport, Resource>(principal.AccountId(), ct));
        routes.MapPost("", (Resource input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(null, input, principal, db, ct)).Produces<Resource>(201);
        routes.MapPut("/{id:guid}", (Guid id, Resource input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(id, input, principal, db, ct)).Produces<Resource>();
    }

    private static async Task<IResult> Save(Guid? id, Resource input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct)
    {
        var accountId = principal.AccountId();
        var plate = input.Plate?.Trim().ToUpperInvariant() ?? "";
        if (plate.Length is < 4 or > 10 || plate.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') ||
            input.Status is not ("draft" or "reviewed" or "published") ||
            input.ServiceType is not ("individual" or "fleet") ||
            input.PaymentStatus is not ("none" or "completed") ||
            input.Make?.Length > 80 || input.Model?.Length > 80 ||
            input.ServiceType == "individual" && (input.PaymentStatus != "completed" || input.VehicleId is not null) ||
            input.ServiceType == "fleet" && (input.PaymentStatus != "none" || input.VehicleId is null) ||
            input.Findings is not { Length: <= 12 } ||
            input.Findings.Any(f => f is null || f.Source is not { Length: > 0 and <= 100 } ||
                f.DateChecked?.Length > 32 || f.Result?.Length > 200 || f.Summary?.Length > 2000 || f.Evidence?.Length > 500))
            return Results.BadRequest(new { message = "Invalid report" });

        if (input.VehicleId is Guid vehicleId)
        {
            var vehicle = await db.Vehicles.AsNoTracking()
                .SingleOrDefaultAsync(v => v.AccountId == accountId && v.Id == vehicleId, ct);
            if (vehicle is null) return Results.NotFound();
            if (id is null) plate = vehicle.Plate;
        }

        var row = id is Guid existing
            ? await db.Owned<VehicleReport>(accountId).SingleOrDefaultAsync(x => x.Id == existing, ct)
            : new VehicleReport { AccountId = accountId };
        if (row is null) return Results.NotFound();
        var previous = id is null ? null : row.Read<Resource>();
        if (previous is not null && (previous.VehicleId != input.VehicleId || previous.Plate != plate ||
            previous.ServiceType != input.ServiceType || previous.PaymentStatus != input.PaymentStatus))
            return Results.BadRequest(new { message = "Report identity cannot change" });

        var now = DateTimeOffset.UtcNow;
        var findings = id is null ? DemoFindings(plate, now.ToString("yyyy-MM-dd")) :
            input.Findings.Select(f => f with { Simulated = true }).ToArray();
        var saved = input with
        {
            Id = row.Id, Plate = plate, Findings = findings, RequestedAt = previous?.RequestedAt ?? now,
            Make = input.Make?.Trim() ?? previous?.Make, Model = input.Model?.Trim() ?? previous?.Model
        };
        row.Write(saved);
        if (id is null) db.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Json(saved, statusCode: id is null ? 201 : 200);
    }

    private static Finding[] DemoFindings(string plate, string date) =>
    [
        new("Consulta Vehicular", date, "Vehiculo registrado", $"Placa {plate}.", "Ficha vehicular", true),
        new("SOAT", date, "SOAT vigente", "La poliza figura vigente a la fecha del reporte.", "Certificado SOAT", true),
        new("SBS Accidentes", date, "Sin accidentes registrados", "No se muestran incidencias para esta placa.", "Historial SBS", true)
    ];

    private sealed record Resource(Guid Id, Guid? VehicleId, string? Plate, string? Status,
        DateTimeOffset? RequestedAt, Finding[]? Findings, string? PaymentStatus, string? ServiceType,
        string? Make = null, string? Model = null);
    private sealed record Finding(string? Source, string? DateChecked, string? Result, string? Summary,
        string? Evidence, bool Simulated);
}
