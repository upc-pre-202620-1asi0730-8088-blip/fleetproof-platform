using Microsoft.EntityFrameworkCore;

namespace FleetProof.SubscriptionManagement;

internal enum SubscriptionAccess
{
    NotCovered,
    Covered
}

internal interface ISubscriptionAccessQuery
{
    Task<SubscriptionAccess> FleetAccessAsync(Guid accountId, CancellationToken ct);
}

internal sealed class SubscriptionAccessQuery(FleetProofDb db) : ISubscriptionAccessQuery
{
    public async Task<SubscriptionAccess> FleetAccessAsync(Guid accountId, CancellationToken ct) =>
        await db.HasPlan(accountId, "fleet", ct) ? SubscriptionAccess.Covered : SubscriptionAccess.NotCovered;
}

internal static class SubscriptionPlanQueries
{
    internal static Task<bool> HasPlan(this FleetProofDb db, Guid accountId, string service, CancellationToken ct) =>
        db.Set<Subscription>().AnyAsync(s => s.AccountId == accountId &&
            (s.Service == service || service == "monitoring" && s.Service == "fleet") &&
            s.Status == "active" && s.PaymentStatus == "completed", ct);
}
