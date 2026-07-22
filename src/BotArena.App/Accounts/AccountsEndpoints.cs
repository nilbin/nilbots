using System.Security.Claims;
using BotArena.App.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Accounts;

public sealed record RegisterRequest(string DisplayName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record CreateTokenRequest(string? Name);
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

            var user = new User { DisplayName = displayName, Email = email, PasswordHash = "" };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await SignIn(http, user);
            return Results.Ok(ToResponse(user));
        }).RequireRateLimiting("auth");

        group.MapPost("/login", async (LoginRequest request, AppDbContext db, HttpContext http) =>
        {
            string email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user is null ||
                new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password)
                    == PasswordVerificationResult.Failed)
                return Results.Problem("Invalid email or password.", statusCode: 401);
            await SignIn(http, user);
            return Results.Ok(ToResponse(user));
        }).RequireRateLimiting("auth");

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = await principal.LoadUserAsync(db);
            return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
        });

        // CLI tokens: created in the browser, shown once, stored hashed.
        group.MapPost("/tokens", async (CreateTokenRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            string name = string.IsNullOrWhiteSpace(request.Name) ? "cli" : request.Name.Trim();
            string plaintext = "ba_" + Convert.ToHexStringLower(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
            db.ApiTokens.Add(new ApiToken { UserId = userId, Name = name, TokenHash = ApiToken.Hash(plaintext) });
            await db.SaveChangesAsync();
            return Results.Ok(new { Token = plaintext, Name = name });
        }).RequireAuthorization();

        group.MapGet("/tokens", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            if (principal.UserId() is not Guid userId)
                return Results.Unauthorized();
            return Results.Ok(await db.ApiTokens.Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Name, t.CreatedAt, t.LastUsedAt })
                .ToListAsync());
        }).RequireAuthorization();
    }

    private static async Task SignIn(HttpContext http, User user)
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
