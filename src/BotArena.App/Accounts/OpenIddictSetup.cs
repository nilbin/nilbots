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
/// The CLI and the mobile app are public clients using Authorization Code + PKCE
/// (§13.2) and refresh tokens; API requests carry the resulting access tokens. The
/// CLI redirects to loopback, the mobile app to its own registered URL scheme.
/// </summary>
public static class OpenIddictSetup
{
    public const string CliClientId = "botarena-cli";

    /// <summary>Loopback callback ports the CLI may bind (registered redirect URIs).</summary>
    public static readonly int[] CliPorts = [43117, 43118, 43119, 43120];

    public const string MobileClientId = "nilbots-mobile";

    /// <summary>
    /// Callback the mobile app receives the authorization code on. The scheme must match
    /// the Expo app's <c>scheme</c> in app.json. OpenIddict compares redirect URIs exactly
    /// and <see cref="Uri"/> appends a trailing slash to an empty path, so this deliberately
    /// carries two path segments — "nilbots://callback" would be stored as
    /// "nilbots://callback/" and never match what the client sends. Pinned by
    /// <c>OpenIddictClientSeedTests</c>; read that test before editing this string.
    /// </summary>
    public const string MobileRedirectUri = "nilbots://auth/callback";

    public static X509Certificate2 LoadEncryptionCertificate(
        IConfiguration configuration,
        bool allowGeneratedCertificates) =>
        LoadCertificate("encryption", configuration, allowGeneratedCertificates);

    public static void AddBotArenaOpenIddict(
        this IServiceCollection services,
        IConfiguration configuration,
        bool includeServer,
        bool allowGeneratedCertificates,
        bool disableTransportSecurityRequirement)
    {
        var builder = services.AddOpenIddict();
        builder.AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>());
        if (!includeServer)
            return;

        X509Certificate2 encryption =
            LoadEncryptionCertificate(configuration, allowGeneratedCertificates);
        X509Certificate2 signing = LoadCertificate(
            "signing", configuration, allowGeneratedCertificates);
        builder
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                       .SetTokenEndpointUris("connect/token");
                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                options.AllowRefreshTokenFlow();
                options.RegisterScopes(Scopes.OfflineAccess);
                options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));

                options.AddEncryptionCertificate(encryption);
                options.AddSigningCertificate(signing);

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough();
                if (disableTransportSecurityRequirement)
                    aspNetCore.DisableTransportSecurityRequirement();
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

    /// <summary>
    /// Registers the first-party public clients (PKCE-required, implicit consent): the CLI
    /// on loopback redirects and the mobile app on its custom scheme.
    /// </summary>
    public static async Task SeedClientAsync(IServiceProvider services)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        await SeedPublicClientAsync(
            manager,
            CliClientId,
            "nilbots CLI",
            [.. CliPorts.Select(port => new Uri($"http://127.0.0.1:{port}/callback/"))]);
        await SeedPublicClientAsync(
            manager,
            MobileClientId,
            "nilbots mobile",
            [new Uri(MobileRedirectUri)]);
    }

    /// <summary>
    /// Creates the client if absent, otherwise reconciles its display name and redirect URIs.
    /// Reconciling matters because a redirect URI is only ever fixed by a deployment: the
    /// migrate role is the sole caller (production web processes never seed).
    /// </summary>
    private static async Task SeedPublicClientAsync(
        IOpenIddictApplicationManager manager,
        string clientId,
        string displayName,
        IReadOnlyList<Uri> redirectUris)
    {
        if (await manager.FindByClientIdAsync(clientId) is { } application)
        {
            var existing = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(existing, application);
            bool changed = false;
            if (existing.DisplayName?.ToString() != displayName)
            {
                existing.DisplayName = displayName;
                changed = true;
            }
            if (!existing.RedirectUris.SetEquals(redirectUris))
            {
                existing.RedirectUris.Clear();
                foreach (var uri in redirectUris)
                    existing.RedirectUris.Add(uri);
                changed = true;
            }
            if (changed)
                await manager.UpdateAsync(application, existing);
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = displayName,
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
        foreach (var uri in redirectUris)
            descriptor.RedirectUris.Add(uri);
        await manager.CreateAsync(descriptor);
    }

    private static X509Certificate2 LoadCertificate(
        string purpose,
        IConfiguration configuration,
        bool allowGeneratedCertificates)
    {
        string setting = $"BOTARENA_OPENIDDICT_{purpose.ToUpperInvariant()}_CERT";
        if (configuration[setting] is { Length: > 0 } configuredPath)
        {
            string path = Path.GetFullPath(configuredPath);
            if (!File.Exists(path))
                throw new InvalidOperationException($"{setting} points to missing file '{path}'.");
            string? password = configuration["BOTARENA_OPENIDDICT_CERT_PASSWORD"];
            return X509CertificateLoader.LoadPkcs12FromFile(
                path,
                password,
                X509KeyStorageFlags.EphemeralKeySet);
        }

        if (!allowGeneratedCertificates)
        {
            throw new InvalidOperationException(
                $"{setting} is required for the production web role. Provision one " +
                "shared certificate pair for every web replica.");
        }
        return LoadOrCreateDevelopmentCertificate(purpose);
    }

    private static X509Certificate2 LoadOrCreateDevelopmentCertificate(string purpose)
    {
        string dir = Path.Combine(DataPaths.Root, "keys");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"openiddict-{purpose}.pfx");
        if (File.Exists(path))
            return X509CertificateLoader.LoadPkcs12FromFile(path, null);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN=nilbots {purpose}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
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
