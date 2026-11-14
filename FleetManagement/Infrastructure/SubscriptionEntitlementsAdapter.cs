using FleetProof.SubscriptionManagement;

namespace FleetProof.FleetManagement.Infrastructure;

internal sealed class SubscriptionEntitlementsAdapter(ISubscriptionAccessQuery subscriptions) : IFleetEntitlements
{
    public async Task<bool> CanManageFleetAsync(Guid accountId, CancellationToken ct) =>
        await subscriptions.FleetAccessAsync(accountId, ct) == SubscriptionAccess.Covered;
}
