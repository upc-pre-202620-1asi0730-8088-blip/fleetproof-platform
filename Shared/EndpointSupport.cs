using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace FleetProof.Shared;

internal static class EndpointSupport
{
    internal static Guid AccountId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    internal static bool IsUniqueViolation(this DbUpdateException error) =>
        error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
