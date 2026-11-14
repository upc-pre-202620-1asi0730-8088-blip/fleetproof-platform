using System.Security.Claims;
using FleetProof.Shared;
using Microsoft.EntityFrameworkCore;

namespace FleetProof.SubscriptionManagement;

internal static class SubscriptionEndpoints
{
    internal static void MapSubscriptions(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/subscriptions").RequireAuthorization();
        routes.MapGet("", async (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
        {
            var accountId = principal.AccountId();
            return await db.Set<Subscription>().AsNoTracking().Where(s => s.AccountId == accountId)
                .Select(s => new Resource(s.Id, s.Service, s.Plan, s.Status, s.PaymentStatus, s.Audience)).ToListAsync(ct);
        });
        routes.MapPost("", (Input input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(null, input, principal, db, ct)).Produces<Resource>(201);
        routes.MapPut("/{id:guid}", (Guid id, Input input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
            Save(id, input, principal, db, ct)).Produces<Resource>(200);
    }

    internal static IResult PlanRequired() => Results.Json(new { message = "Active plan required" }, statusCode: 403);

    private static async Task<IResult> Save(Guid? id, Input input, ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct)
    {
        if (input.Service is not ("fleet" or "monitoring") || input.Plan is not ("weekly" or "monthly" or "annual") ||
            input.Audience is not (null or "personal" or "business") ||
            (input.Status, input.PaymentStatus) is not (("active", "completed") or
                ("pending", "pending") or ("pending", "failed") or ("cancelled", "completed") or
                ("cancelled", "pending") or ("cancelled", "failed")))
            return Results.BadRequest(new { message = "Invalid simulated subscription" });

        var accountId = principal.AccountId();
        var subscriptions = db.Set<Subscription>();
        var subscription = id is null
            ? await subscriptions.SingleOrDefaultAsync(s => s.AccountId == accountId && s.Service == input.Service, ct)
            : await subscriptions.SingleOrDefaultAsync(s => s.AccountId == accountId && s.Id == id, ct);
        if (id is not null && subscription is null)
            return Results.NotFound();
        if (subscription is not null && (subscription.Service != input.Service ||
            id is null && subscription.Status != "cancelled"))
            return Results.Conflict(new { message = "Subscription already exists or service cannot change" });
        if (subscription is null)
        {
            subscription = new Subscription { AccountId = accountId, Service = input.Service };
            subscriptions.Add(subscription);
        }
        subscription.Plan = input.Plan;
        subscription.Audience = input.Audience ?? subscription.Audience;
        subscription.Status = input.Status!;
        subscription.PaymentStatus = input.PaymentStatus!;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        { return Results.Conflict(new { message = "Subscription already exists" }); }

        return Results.Json(new Resource(subscription.Id, subscription.Service, subscription.Plan,
            subscription.Status, subscription.PaymentStatus, subscription.Audience), statusCode: id is null ? 201 : 200);
    }

    private sealed record Input(string? Service, string? Plan, string? Status, string? PaymentStatus, string? Audience);
    private sealed record Resource(Guid Id, string Service, string Plan, string Status, string PaymentStatus, string Audience);
}
