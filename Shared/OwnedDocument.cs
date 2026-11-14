using System.Text.Json;
using FleetProof.UserManagement;
using Microsoft.EntityFrameworkCore;

namespace FleetProof.Shared;

public interface IOwnedDocument
{
    Guid Id { get; init; }
    Guid AccountId { get; init; }
    string Data { get; set; }
}

internal static class OwnedDocuments
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static void ConfigureDocument<T>(this ModelBuilder builder, string table)
        where T : class, IOwnedDocument
    {
        var entity = builder.Entity<T>();
        entity.ToTable(table);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.AccountId).HasColumnName("user_id");
        entity.Property(x => x.Data).HasColumnName("data").HasColumnType("jsonb");
        entity.HasIndex(x => x.AccountId);
        entity.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
    }

    internal static IQueryable<T> Owned<T>(this FleetProofDb db, Guid accountId)
        where T : class, IOwnedDocument => db.Set<T>().Where(x => x.AccountId == accountId);

    internal static TValue Read<TValue>(this IOwnedDocument document) =>
        JsonSerializer.Deserialize<TValue>(document.Data, Json)!;

    internal static void Write<TValue>(this IOwnedDocument document, TValue value) =>
        document.Data = JsonSerializer.Serialize(value, Json);

    internal static async Task<TValue[]> List<T, TValue>(this FleetProofDb db, Guid accountId, CancellationToken ct)
        where T : class, IOwnedDocument
    {
        var rows = await db.Owned<T>(accountId).AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        return rows.Select(x => x.Read<TValue>()).ToArray();
    }
}
