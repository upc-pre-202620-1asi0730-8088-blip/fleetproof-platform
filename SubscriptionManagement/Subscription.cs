using FleetProof.UserManagement;
using Microsoft.EntityFrameworkCore;

namespace FleetProof.SubscriptionManagement;

public sealed class Subscription
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AccountId { get; init; }
    public required string Service { get; init; }
    public string Plan { get; set; } = "monthly";
    public string Audience { get; set; } = "business";
    public string Status { get; set; } = "pending";
    public string PaymentStatus { get; set; } = "pending";

    internal static void Configure(ModelBuilder builder)
    {
        var entity = builder.Entity<Subscription>();
        entity.ToTable("subscriptions");
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.AccountId).HasColumnName("user_id");
        entity.Property(x => x.Service).HasColumnName("service").HasMaxLength(16);
        entity.Property(x => x.Plan).HasColumnName("plan").HasMaxLength(16);
        entity.Property(x => x.Audience).HasColumnName("audience").HasMaxLength(16).HasDefaultValue("business");
        entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
        entity.Property(x => x.PaymentStatus).HasColumnName("payment_status").HasMaxLength(16);
        entity.HasIndex(x => new { x.AccountId, x.Service }).IsUnique();
        entity.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
    }
}
