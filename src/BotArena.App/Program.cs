using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Shared;
using BotArena.Engine;
using BotArena.Toolchain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("BotArena")
    ?? Environment.GetEnvironmentVariable("BOTARENA_DB")
    ?? "Host=127.0.0.1;Database=botarena;Username=botarena;Password=botarena";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseOpenIddict();
    // OpenIddict's EF model trips EF 10's pending-changes heuristic with no actual diff.
    options.ConfigureWarnings(w => w.Ignore(
        Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddBotArenaOpenIddict();
builder.Services
    .AddAuthentication("CookieOrBearer")
    .AddPolicyScheme("CookieOrBearer", "Cookie or access token", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString().StartsWith("Bearer ")
                ? OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "botarena.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // An API returns status codes, not login-page redirects.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
            }));
    // Credential endpoints: slow brute force.
    options.AddPolicy("auth", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
            }));
    // Compilation is expensive: a handful of submissions per user per ten minutes.
    options.AddPolicy("submission", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(10),
            }));
    options.AddPolicy("challenge", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
            }));
});
builder.Services.AddHostedService<JobWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await BuiltInBotSeeder.SeedAsync(db);
    await ChampionSeeder.SeedAsync(db);
    await OpenIddictSetup.SeedClientAsync(scope.ServiceProvider);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAccounts();
app.MapConnect();
app.MapBots();
app.MapMatches();
app.MapRanked();

app.MapGet("/api/meta", () =>
{
    var maps = new List<object>();
    if (RepoPaths.FindUpward("maps") is { } mapsDir)
    {
        foreach (var file in Directory.EnumerateFiles(mapsDir, "*.json").Order())
        {
            var map = ArenaMap.FromJson(File.ReadAllText(file));
            maps.Add(new { map.Id, map.Width, map.Height });
        }
    }
    return Results.Ok(new
    {
        EngineVersion = BotArenaVersions.EngineVersion,
        GameRulesVersion = BotArenaVersions.GameRulesVersion,
        RuntimeProtocolVersion = BotArenaVersions.RuntimeProtocolVersion,
        SdkVersion = ToolchainInfo.SdkVersion,
        Maps = maps,
    });
});

// The SPA: a single self-contained index.html built from web/ (`npm run build`).
// Served straight from web/dist so dev and Docker need no copy step.
string? spaDir = RepoPaths.FindUpward(Path.Combine("web", "dist"));
if (spaDir is not null && File.Exists(Path.Combine(spaDir, "index.html")))
{
    var spaFiles = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(spaDir);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = spaFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFiles });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = spaFiles });
}

app.Run();
