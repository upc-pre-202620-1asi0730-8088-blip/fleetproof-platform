namespace FleetProof.VehicleInformation;

public sealed class Vehicle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public string Plate { get; set; } = "";
    public string Type { get; set; } = "";
    public string Site { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Risk { get; set; } = "low";
    public bool Monitored { get; set; }
}
