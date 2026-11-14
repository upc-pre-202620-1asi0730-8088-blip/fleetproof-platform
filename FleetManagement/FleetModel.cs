using FleetProof.UserManagement;
using FleetProof.VehicleInformation;
using Microsoft.EntityFrameworkCore;

namespace FleetProof.FleetManagement;

internal static class FleetModel
{
    internal static void ConfigureFleetManagement(this ModelBuilder builder)
    {
        builder.Entity<Fleet>(entity =>
        {
            entity.ToTable("fleets");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.AccountId).HasColumnName("user_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(120);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
        });
        builder.Entity<FleetAssignment>(entity =>
        {
            entity.ToTable("fleet_vehicle_assignments");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.AccountId).HasColumnName("user_id");
            entity.Property(x => x.FleetId).HasColumnName("fleet_id");
            entity.Property(x => x.VehicleId).HasColumnName("vehicle_id");
            entity.Property(x => x.Responsible).HasColumnName("responsible").HasMaxLength(120);
            entity.HasIndex(x => new { x.AccountId, x.VehicleId }).IsUnique();

            // Composite foreign keys keep both references in the same account.
            entity.HasOne<Fleet>().WithMany()
                .HasForeignKey(x => new { x.AccountId, x.FleetId })
                .HasPrincipalKey(x => new { x.AccountId, x.Id });
            entity.HasOne<Vehicle>().WithMany()
                .HasForeignKey(x => new { x.AccountId, x.VehicleId })
                .HasPrincipalKey(x => new { x.AccountId, x.Id });
        });
    }
}
