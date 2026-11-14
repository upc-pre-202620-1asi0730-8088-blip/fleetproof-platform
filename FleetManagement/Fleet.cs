namespace FleetProof.FleetManagement;

public sealed class Fleet
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public required string Name { get; init; }
}

public sealed class FleetAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public Guid FleetId { get; init; }
    public Guid VehicleId { get; init; }
    public required string Responsible { get; init; }
}
