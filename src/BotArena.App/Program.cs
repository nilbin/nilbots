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

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
builder.Services.AddHostedService<JobWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await BuiltInBotSeeder.SeedAsync(db);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAccounts();
app.MapBots();
app.MapMatches();

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
