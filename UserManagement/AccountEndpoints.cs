using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FleetProof.Shared;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace FleetProof.UserManagement;

internal static partial class AccountEndpoints
{
    public static void MapAccounts(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/authentication").RequireRateLimiting("authentication");
        auth.MapPost("/sign-up", SignUp)
            .Produces(201).Produces(400).Produces(409).Produces(429);
        auth.MapPost("/sign-in", SignIn)
            .Produces<Session>(200).Produces(401).Produces(429);
        api.MapGet("/account", async (ClaimsPrincipal principal, FleetProofDb db, CancellationToken ct) =>
        {
            var account = await db.Accounts.FindAsync([principal.AccountId()], ct);
            return account is null ? Results.Unauthorized()
                : Results.Ok(new { account.Id, account.Username, Role = "user" });
        }).RequireAuthorization();
        api.MapPatch("/account", UpdateAccount).RequireAuthorization();
        api.MapDelete("/account", DeleteAccount).RequireAuthorization();
    }

    internal static async Task CheckSession(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var id = context.User.AccountId();
            var version = context.User.FindFirstValue("session_version");
            var db = context.RequestServices.GetRequiredService<FleetProofDb>();
            var current = await db.Accounts.Where(a => a.Id == id)
                .Select(a => (int?)a.SessionVersion).SingleOrDefaultAsync(context.RequestAborted);
            if (current is null || version != current.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }
        await next(context);
    }

    private static async Task<IResult> UpdateAccount(AccountUpdate input, ClaimsPrincipal principal,
        FleetProofDb db, IPasswordHasher<Account> hasher, CancellationToken ct)
    {
        var account = await db.Accounts.FindAsync([principal.AccountId()], ct);
        if (account is null) return Results.Unauthorized();
        if (!Matches(account, input.CurrentPassword, hasher)) return IncorrectPassword();
        var username = input.Username?.Trim() ?? "";
        var changePassword = !string.IsNullOrEmpty(input.Password);
        if (!UsernamePattern().IsMatch(username) || changePassword && input.Password is not { Length: >= 8 and <= 128 })
            return Results.BadRequest(new { message = "Invalid account details" });
        account.Username = username;
        account.NormalizedUsername = username.ToUpperInvariant();
        if (changePassword)
        {
            account.PasswordHash = hasher.HashPassword(account, input.Password!);
            account.SessionVersion++;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        { return Results.Conflict(new { message = "Username already exists" }); }
        return Results.Ok(new { account.Id, account.Username, Role = "user", SignInRequired = changePassword });
    }

    private static async Task<IResult> DeleteAccount([Microsoft.AspNetCore.Mvc.FromBody] AccountUpdate input,
        ClaimsPrincipal principal, FleetProofDb db, IPasswordHasher<Account> hasher, CancellationToken ct)
    {
        var account = await db.Accounts.FindAsync([principal.AccountId()], ct);
        if (account is null) return Results.Unauthorized();
        if (!Matches(account, input.CurrentPassword, hasher)) return IncorrectPassword();
        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static bool Matches(Account account, string? password, IPasswordHasher<Account> hasher) =>
        password is { Length: >= 8 and <= 128 } &&
        hasher.VerifyHashedPassword(account, account.PasswordHash, password) != PasswordVerificationResult.Failed;

    private static IResult IncorrectPassword() =>
        Results.Json(new { message = "Current password is incorrect" }, statusCode: 403);

    private static async Task<IResult> SignUp(Credentials input, FleetProofDb db,
        IPasswordHasher<Account> hasher, CancellationToken ct)
    {
        var username = input.Username?.Trim() ?? "";
        if (!UsernamePattern().IsMatch(username) || input.Password is not { Length: >= 8 and <= 128 })
            return Results.BadRequest(new { message = "Invalid registration" });

        var account = new Account { Username = username, NormalizedUsername = username.ToUpperInvariant() };
        account.PasswordHash = hasher.HashPassword(account, input.Password);
        db.Accounts.Add(account);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.IsUniqueViolation())
        {
            return Results.Conflict(new { message = "Username already exists" });
        }
        return Results.Json(new { message = "Account created" }, statusCode: 201);
    }

    private static async Task<IResult> SignIn(Credentials input, FleetProofDb db,
        IPasswordHasher<Account> hasher, IOptionsSnapshot<BearerTokenOptions> options, CancellationToken ct)
    {
        var username = input.Username?.Trim() ?? "";
        if (!UsernamePattern().IsMatch(username) || input.Password is not { Length: >= 8 and <= 128 })
            return InvalidCredentials();

        var normalized = username.ToUpperInvariant();
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.NormalizedUsername == normalized, ct);
        if (account is null)
            return InvalidCredentials();

        var verification = hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password);
        if (verification == PasswordVerificationResult.Failed)
            return InvalidCredentials();
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = hasher.HashPassword(account, input.Password);
            await db.SaveChangesAsync(ct);
        }

        var scheme = BearerTokenDefaults.AuthenticationScheme;
        var settings = options.Get(scheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
             new Claim("session_version", account.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))], scheme));
        var properties = new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow.Add(settings.BearerTokenExpiration)
        };
        var token = settings.BearerTokenProtector.Protect(new AuthenticationTicket(principal, properties, scheme));
        return Results.Ok(new Session(account.Id, account.Username, "user", token));
    }

    private static IResult InvalidCredentials() =>
        Results.Json(new { message = "Invalid credentials" }, statusCode: 401);

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$")]
    private static partial Regex UsernamePattern();

    private sealed record Credentials(string? Username, string? Password);
    private sealed record AccountUpdate(string? Username, string? Password, string? CurrentPassword);
    private sealed record Session(Guid Id, string Username, string Role, string Token);
}
