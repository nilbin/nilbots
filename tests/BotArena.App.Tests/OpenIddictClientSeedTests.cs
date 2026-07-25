using BotArena.App.Accounts;

namespace BotArena.App.Tests;

/// <summary>
/// OpenIddict compares redirect URIs exactly, and <see cref="Uri"/> rewrites custom-scheme
/// strings when it parses them. A mismatch between what the mobile app sends and what the
/// seeder stored fails at the authorize endpoint with an opaque invalid_request, so the
/// serialized form is pinned here rather than discovered in production.
/// </summary>
public class OpenIddictClientSeedTests
{
    [Fact]
    public void MobileRedirectUri_SurvivesUriNormalizationUnchanged()
    {
        // If this fails, Uri rewrote the literal (most likely appending a trailing slash).
        // Fix by making OpenIddictSetup.MobileRedirectUri the normalized form AND making
        // the Expo client send exactly that — not by relaxing this assertion.
        Assert.Equal(
            OpenIddictSetup.MobileRedirectUri,
            new Uri(OpenIddictSetup.MobileRedirectUri).AbsoluteUri);
    }

    [Fact]
    public void MobileRedirectUri_UsesTheSchemeRegisteredByTheExpoApp()
    {
        // Must equal `expo.scheme` in mobile/app.json.
        Assert.Equal("nilbots", new Uri(OpenIddictSetup.MobileRedirectUri).Scheme);
    }

    [Fact]
    public void MobileClient_IsDistinctFromTheCliClient()
    {
        Assert.NotEqual(OpenIddictSetup.CliClientId, OpenIddictSetup.MobileClientId);
    }
}
