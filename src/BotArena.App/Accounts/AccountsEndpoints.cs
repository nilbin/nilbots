using System.Security.Claims;
using BotArena.App.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Accounts;

public sealed record RegisterRequest(string DisplayName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record UserResponse(Guid Id, string DisplayName, string Email);

public static class AccountsEndpoints
{
    public static void MapAccounts(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/accounts");

        group.MapPost("/register", async (RegisterRequest request, AppDbContext db, HttpContext http) =>
        {
            string displayName = request.DisplayName.Trim();
            string email = request.Email.Trim().ToLowerInvariant();
            if (displayName.Length is < 2 or > 40)
                return Results.Problem("Display name must be 2-40 characters.", statusCode: 400);
            if (!email.Contains('@') || email.Length > 200)
                return Results.Problem("Invalid email address.", statusCode: 400);
            if (request.Password.Length < 8)
                return Results.Problem("Password must be at least 8 characters.", statusCode: 400);
            if (await db.Users.AnyAsync(u => u.Email == email))
                return Results.Problem("An account with this email already exists.", statusCode: 409);
            // Rejected rather than silently altered. A name typed into a form is a choice,
            // and quietly handing back "Pincer2" is how someone ends up on the ladder under
            // a name they did not pick. The external-provider path suffixes instead,
            // because there is no form and no one to ask (DECISIONS #121).
            if (await DisplayNames.IsTakenAsync(db, displayName, default))
                return Results.Problem("That display name is taken.", statusCode: 409);

            var user = new User { DisplayName = displayName, Email = email };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await SignInAsync(http, user);
            return Results.Ok(ToResponse(user));
        }).Produces<UserResponse>().RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/login", async (LoginRequest request, AppDbContext db, HttpContext http) =>
        {
            string email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
            // A passwordless account — one that has only ever signed in through Google —
            // is refused before the verifier sees it. The message stays deliberately
            // identical to a wrong password: telling an anonymous caller "that address
            // exists but uses Google" is an account-enumeration oracle, and the person it
            // would help most is the one guessing.
            if (user?.PasswordHash is not { Length: > 0 } hash ||
                new PasswordHasher<User>().VerifyHashedPassword(user, hash, request.Password)
                    == PasswordVerificationResult.Failed)
                return Results.Problem("Invalid email or password.", statusCode: 401);
            await SignInAsync(http, user);
            return Results.Ok(ToResponse(user));
        }).Produces<UserResponse>().RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await principal.LoadUserAsync(db);
            return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
        }).Produces<UserResponse>();
    }

    /// <summary>
    /// Issue the session cookie. Shared rather than private, because an external provider
    /// must produce exactly the same session a password login does — anything else would
    /// make "signed in" mean two different things to everything downstream.
    /// </summary>
    public static async Task SignInAsync(HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }

    private static UserResponse ToResponse(User user) => new(user.Id, user.DisplayName, user.Email);

    public static Guid? UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static async Task<User?> LoadUserAsync(this ClaimsPrincipal principal, AppDbContext db) =>
        principal.UserId() is Guid id ? await db.Users.FindAsync(id) : null;
}
