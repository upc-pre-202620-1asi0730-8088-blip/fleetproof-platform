using System.Security.Claims;
using FleetProof.ReportManagement;
using FleetProof.Shared;
using Microsoft.EntityFrameworkCore;

namespace FleetProof.FleetManagement;

public sealed class ResolutionCase : IOwnedDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public string Data { get; set; } = "{}";
}

internal static class CaseEndpoints
{
    internal static void MapCases(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/cases").RequireAuthorization();
        routes.MapGet("", (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            db.List<ResolutionCase, Resource>(principal.AccountId(), ct));
        routes.MapPost("", (Resource input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(null, input, principal, db, ct)).Produces<Resource>(201);
        routes.MapPut("/{id:guid}", (Guid id, Resource input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(id, input, principal, db, ct)).Produces<Resource>();
    }

    private static async Task<IResult> Save(Guid? id, Resource input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct)
    {
        var plate = input.Plate?.Trim().ToUpperInvariant() ?? "";
        var title = input.Title?.Trim() ?? "";
        var note = input.Note?.Trim() ?? "";
        var evidence = input.Evidence?.Trim() ?? "";
        if (plate.Length is < 1 or > 10 || title.Length is < 1 or > 200 ||
            input.Status is not ("open" or "inProgress" or "resolved") ||
            input.Severity is not ("low" or "medium" or "high") ||
            input.Responsible?.Length > 120 || note.Length > 2000 || evidence.Length > 1000 ||
            (evidence.Length > 0 && (!Uri.TryCreate(evidence, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host))) ||
            (!string.IsNullOrEmpty(input.DueDate) && !DateOnly.TryParseExact(input.DueDate, "yyyy-MM-dd", out _)) ||
            (input.Status == "resolved" && (note.Length == 0 || evidence.Length == 0)))
            return Results.BadRequest(new { message = "Invalid resolution case" });

        var accountId = principal.AccountId();
        if (input.ReportId is Guid reportId &&
            !await db.Owned<VehicleReport>(accountId).AnyAsync(x => x.Id == reportId, ct))
            return Results.NotFound();
        var row = id is Guid existing
            ? await db.Owned<ResolutionCase>(accountId).SingleOrDefaultAsync(x => x.Id == existing, ct)
            : new ResolutionCase { AccountId = accountId };
        if (row is null) return Results.NotFound();
        var saved = input with { Id = row.Id, Plate = plate, Title = title, Evidence = evidence, Note = note,
            Responsible = input.Responsible?.Trim() ?? "", DueDate = input.DueDate ?? "" };
        row.Write(saved);
        if (id is null) db.Add(row);
        await db.SaveChangesAsync(ct);
        return Results.Json(saved, statusCode: id is null ? 201 : 200);
    }

    private sealed record Resource(Guid Id, Guid? ReportId, string? Plate, string? Title, string? Severity,
        string? Status, string? Responsible, string? DueDate, string? Evidence, string? Note);
}
