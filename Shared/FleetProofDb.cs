using Microsoft.EntityFrameworkCore;
using FleetProof.FleetManagement;
using FleetProof.SubscriptionManagement;
using FleetProof.ReportManagement;
using FleetProof.Shared;
using FleetProof.VehicleMonitoring;
using FleetProof.UserManagement;
using FleetProof.VehicleInformation;

namespace FleetProof;

public sealed class FleetProofDb(DbContextOptions<FleetProofDb> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Fleet> Fleets => Set<Fleet>();
    public DbSet<FleetAssignment> Assignments => Set<FleetAssignment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Account>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(64);
            entity.Property(x => x.NormalizedUsername).HasColumnName("normalized_username").HasMaxLength(64);
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.SessionVersion).HasColumnName("session_version");
            entity.HasIndex(x => x.NormalizedUsername).IsUnique();
        });
        builder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("vehicles");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.AccountId).HasColumnName("user_id");
            entity.Property(x => x.Plate).HasColumnName("plate").HasMaxLength(10);
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(80);
            entity.Property(x => x.Site).HasColumnName("site").HasMaxLength(100);
            entity.Property(x => x.Owner).HasColumnName("owner").HasMaxLength(120);
            entity.Property(x => x.Risk).HasColumnName("risk").HasMaxLength(12);
            entity.Property(x => x.Monitored).HasColumnName("monitored");
            entity.HasIndex(x => new { x.AccountId, x.Plate }).IsUnique();
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.ConfigureFleetManagement();
        Subscription.Configure(builder);
        builder.ConfigureDocument<VehicleReport>("vehicle_reports");
        builder.ConfigureDocument<ResolutionCase>("resolution_cases");
        builder.ConfigureMonitoring();
    }
}
