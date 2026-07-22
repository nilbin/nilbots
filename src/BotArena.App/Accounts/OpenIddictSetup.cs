using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BotArena.App.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BotArena.App.Accounts;

/// <summary>
/// OpenIddict embedded in the monolith (plan §13): same database, no identity service.
/// The CLI is a public client using Authorization Code + PKCE with loopback redirects
/// (§13.2) and refresh tokens; API requests carry the resulting access tokens.
/// </summary>
public static class OpenIddictSetup
{
    public const string CliClientId = "botarena-cli";

    /// <summary>Loopback callback ports the CLI may bind (registered redirect URIs).</summary>
    public static readonly int[] CliPorts = [43117, 43118, 43119, 43120];

    public static void AddBotArenaOpenIddict(this IServiceCollection services)
    {
        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>())
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                       .SetTokenEndpointUris("connect/token");
                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                options.AllowRefreshTokenFlow();
                options.RegisterScopes(Scopes.OfflineAccess);
                options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));

                // Keys persist under BOTARENA_DATA so tokens survive restarts/redeploys.
                options.AddEncryptionCertificate(LoadOrCreateCertificate("encryption"));
                options.AddSigningCertificate(LoadOrCreateCertificate("signing"));

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .DisableTransportSecurityRequirement(); // TLS terminates at the reverse proxy.
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });
    }

    public static void MapConnect(this IEndpointRouteBuilder routes)
    {
        routes.MapMethods("/connect/authorize", ["GET", "POST"], async (HttpContext context, AppDbContext db) =>
        {
            var request = context.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("Not an OpenIddict request.");
            var cookie = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!cookie.Succeeded)
            {
                // Send the user through the SPA login, then back here to finish the grant.
                return Results.Redirect("/login?returnUrl=" + Uri.EscapeDataString(
                    context.Request.Path + context.Request.QueryString));
            }

            string userId = cookie.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var user = await db.Users.FindAsync(Guid.Parse(userId));
            if (user is null)
                return Results.Forbid();

            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(Claims.Subject, userId));
            identity.AddClaim(new Claim(Claims.Name, user.DisplayName));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            identity.SetScopes(request.GetScopes());
            identity.SetDestinations(claim => [Destinations.AccessToken]);
            return Results.SignIn(new ClaimsPrincipal(identity),
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        });

        routes.MapPost("/connect/token", async (HttpContext context) =>
        {
            var request = context.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("Not an OpenIddict request.");
            if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
                return Results.BadRequest("Unsupported grant type.");
            var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            return Results.SignIn(result.Principal!,
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        });
    }

    /// <summary>Registers the first-party CLI client (public, PKCE-required, implicit consent).</summary>
    public static async Task SeedClientAsync(IServiceProvider services)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        if (await manager.FindByClientIdAsync(CliClientId) is not null)
            return;
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = CliClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Bot Arena CLI",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (int port in CliPorts)
            descriptor.RedirectUris.Add(new Uri($"http://127.0.0.1:{port}/callback/"));
        await manager.CreateAsync(descriptor);
    }

    private static X509Certificate2 LoadOrCreateCertificate(string purpose)
    {
        string dir = Path.Combine(DataPaths.Root, "keys");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"openiddict-{purpose}.pfx");
        if (File.Exists(path))
            return X509CertificateLoader.LoadPkcs12FromFile(path, null);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN=BotArena {purpose}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            purpose == "signing" ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment,
            critical: true));
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return certificate;
    }
}
