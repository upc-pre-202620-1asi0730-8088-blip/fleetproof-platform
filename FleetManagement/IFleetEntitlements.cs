namespace FleetProof.FleetManagement;

internal interface IFleetEntitlements
{
    Task<bool> CanManageFleetAsync(Guid accountId, CancellationToken ct);
}
