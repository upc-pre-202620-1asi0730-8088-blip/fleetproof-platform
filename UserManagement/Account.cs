namespace FleetProof.UserManagement;

public sealed class Account
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Username { get; set; }
    public required string NormalizedUsername { get; set; }
    public string PasswordHash { get; set; } = "";
    public int SessionVersion { get; set; }
}
