using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BotArena.App.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BotArena.App.Accounts;

/// <summary>
/// CLI credential (pilot stand-in for the plan's §13.2 OpenIddict PKCE flow — see
/// docs/DECISIONS.md). Only the SHA-256 of the token is stored; the plaintext is
/// shown once at creation.
/// </summary>
public class ApiToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Authenticates `Authorization: Bearer ba_...` requests for the CLI.</summary>
public sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "ApiToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (header is null || !header.StartsWith("Bearer ba_", StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        string hash = ApiToken.Hash(header["Bearer ".Length..].Trim());
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = await db.Set<ApiToken>().SingleOrDefaultAsync(t => t.TokenHash == hash);
        if (token is null)
            return AuthenticateResult.Fail("Unknown token.");
        var user = await db.Users.FindAsync(token.UserId);
        if (user is null)
            return AuthenticateResult.Fail("Token owner no longer exists.");

        token.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
            ],
            Scheme);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }
}
