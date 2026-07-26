using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Client.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BotArena.App.Accounts;

/// <summary>Which external providers this deployment can actually offer.</summary>
public sealed record AuthProvidersResponse(bool Google);

/// <summary>
/// Signing in with Google, through the OpenIddict client.
/// <para>
/// The callback ends in exactly the same cookie the password flow issues, and that is the
/// whole design: <c>/connect/authorize</c> authenticates from that cookie and bounces to
/// the SPA login when it is absent, so the mobile app and the CLI — which both open the
/// site in a browser to sign in — inherit Google with no client change at all.
/// </para>
/// </summary>
public static class ExternalAuthEndpoints
{
    /// <summary>Matches the provider name OpenIddict's web integration registers.</summary>
    private const string GoogleProvider = "Google";

    public static void MapExternalAuth(this IEndpointRouteBuilder routes)
    {
        // What the site should render. A deployment with no Google credentials configured
        // must not show a button that can only fail.
        routes.MapGet("/api/accounts/providers", (IConfiguration configuration) =>
            Results.Ok(new AuthProvidersResponse(GoogleAuthOptions.IsConfigured(configuration))))
            .Produces<AuthProvidersResponse>()
            .AllowAnonymous();

        // Start the flow. A redirect rather than an API call, because the browser has to
        // leave for Google and come back — which is also why the site navigates here
        // instead of fetching it.
        routes.MapGet("/api/accounts/external/google", (
            string? returnUrl,
            IConfiguration configuration) =>
        {
            if (!GoogleAuthOptions.IsConfigured(configuration))
                return Results.NotFound("Google sign-in is not configured on this server.");

            var properties = new AuthenticationProperties
            {
                // Where to land afterwards. Sanitised on the way in and again on the way
                // out: an open redirect here would let a phishing page borrow our domain to
                // bounce someone anywhere, holding a real session cookie.
                RedirectUri = SafeReturnUrl(returnUrl),
            };
            properties.SetString(
                OpenIddictClientAspNetCoreConstants.Properties.ProviderName,
                GoogleProvider);

            return Results.Challenge(
                properties,
                [OpenIddictClientAspNetCoreDefaults.AuthenticationScheme]);
        }).AllowAnonymous().RequireRateLimiting("auth");

        // Where Google returns. The path is fixed by the client's registered redirect URI,
        // and passthrough is what lets this handler run instead of OpenIddict answering.
        routes.MapMethods("/callback/login/google", ["GET", "POST"], async (
            HttpContext http,
            ExternalSignInService signIn,
            CancellationToken cancellationToken) =>
        {
            AuthenticateResult result = await http.AuthenticateAsync(
                OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal is null)
                return Results.Redirect("/login?error=google");

            // The provider's own claims, not our issued ones. OpenIddict merges the
            // identity token and userinfo, so `sub` is present for any conformant provider.
            string? subject = result.Principal.FindFirstValue(Claims.Subject);
            string? email = result.Principal.FindFirstValue(Claims.Email);
            if (subject is null || email is null)
                return Results.Redirect("/login?error=google");

            var identity = new ExternalIdentity(
                ExternalLoginProviders.Google,
                subject,
                email,
                result.Principal.FindFirstValue(Claims.Name),
                // Absent counts as unverified: the safe reading of "did not say" is "no",
                // and the linking rule turns on this exact bit.
                EmailVerified: string.Equals(
                    result.Principal.FindFirstValue(Claims.EmailVerified),
                    "true",
                    StringComparison.OrdinalIgnoreCase));

            ExternalSignInOutcome outcome = await signIn.SignInAsync(identity, cancellationToken);
            if (outcome.User is null)
                return Results.Redirect($"/login?error={outcome.Error}");

            await AccountsEndpoints.SignInAsync(http, outcome.User);

            string returnUrl = result.Properties?.RedirectUri is { Length: > 0 } target
                ? SafeReturnUrl(target)
                : "/garage";
            return Results.Redirect(returnUrl);
        }).AllowAnonymous();
    }

    /// <summary>
    /// Only same-site paths, ever.
    /// <para>
    /// This value survives a round trip through Google and ends in a redirect issued while
    /// the user holds a fresh session cookie — exactly the shape of an open-redirect phish.
    /// A scheme-relative <c>//evil.example</c> is treated as absolute by browsers, so the
    /// test is "one slash, and the next character is not another", not "starts with a
    /// slash".
    /// </para>
    /// </summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 1 } candidate
        && candidate[0] == '/'
        && candidate[1] != '/'
        && candidate[1] != '\\'
            ? candidate
            : "/garage";
}

public static class GoogleAuthOptions
{
    public const string Section = "Authentication:Google";

    /// <summary>
    /// Whether credentials are present.
    /// <para>
    /// Absent is the normal case for local development and every test run — the OpenIddict
    /// client is not registered at all, the button is not rendered, and the endpoints 404
    /// rather than half-working.
    /// </para>
    /// </summary>
    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[$"{Section}:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration[$"{Section}:ClientSecret"]);
}
