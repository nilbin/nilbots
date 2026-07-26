using System.Security.Claims;
using BotArena.App.Accounts;
using BotArena.App.Bots;
using BotArena.App.Cosmetics;
using BotArena.App.Jobs;
using BotArena.App.Matches;
using BotArena.App.Notifications;
using BotArena.App.Shared;
using BotArena.App.Storage;
using BotArena.Engine;
using BotArena.Toolchain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = CompilerSubmissionLimits.MaxRequestBodyBytes);
// Microsoft.Extensions.ApiDescription.Server loads this entry point inside its
// GetDocument.Insider host. That process must describe the web surface without
// running the local `all` role, migrations, or infrastructure-backed listeners.
bool generatingOpenApiDocument = string.Equals(
    System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name,
    "GetDocument.Insider",
    StringComparison.Ordinal);
var mode = ApplicationMode.Parse(
    generatingOpenApiDocument ? "web" : builder.Configuration["BOTARENA_ROLE"]);
bool trustForwardedHeaders =
    builder.Configuration.GetValue<bool>("BOTARENA_TRUST_FORWARDED_HEADERS");

builder.Services.AddSingleton(mode);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(MatchExecutionSettings.FromEnvironment());
builder.Services.AddObjectStore(builder.Configuration);
builder.Services.AddSingleton(BuildProvenance.FromConfiguration(builder.Configuration));
if (mode.RunsCompileWorker && !mode.IsAll)
    builder.Services.AddSingleton<ISubmissionCompiler, QueuedSubmissionCompiler>();
else
    builder.Services.AddSingleton<ISubmissionCompiler, DirectSubmissionCompiler>();

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
builder.Services.AddSingleton(CosmeticCatalog.LoadDefault());
builder.Services.AddScoped<CosmeticEntitlementService>();
builder.Services.AddScoped<CosmeticAchievementService>();
builder.Services.AddScoped<BotAppearancePolicy>();
builder.Services.AddScoped<MatchAdmissionService>();
builder.Services.AddSingleton<MatchParticipantSnapshotFactory>();
builder.Services.AddScoped<BackgroundJobLeaseStore>();
builder.Services.AddScoped<BackgroundJobDispatcher>();
builder.Services.AddScoped<CompileSubmissionJobHandler>();
builder.Services.AddScoped<MatchExecutionJobHandler>();
builder.Services.AddScoped<AnnounceMatchResultJobHandler>();
builder.Services.AddScoped<AnnounceSetResultJobHandler>();
builder.Services.AddScoped<UserNotificationWriter>();
builder.Services.AddScoped<MatchReplayWriter>();
builder.Services.AddScoped<RankedMatchSetFinalizer>();

if (mode.RunsWeb)
{
    bool developmentAuth =
        builder.Environment.IsDevelopment() || mode.IsAll || generatingOpenApiDocument;
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("BotArena");
    if (!generatingOpenApiDocument)
    {
        dataProtection
            .PersistKeysToDbContext<AppDbContext>()
            .ProtectKeysWithCertificate(OpenIddictSetup.LoadEncryptionCertificate(
                builder.Configuration,
                developmentAuth));
    }
    builder.Services.AddBotArenaOpenIddict(
        builder.Configuration,
        includeServer: true,
        allowGeneratedCertificates: developmentAuth,
        disableTransportSecurityRequirement: developmentAuth);
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
            options.Cookie.SecurePolicy = developmentAuth
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
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
    // Published so typed clients are generated from the server rather than hand-mirrored.
    // The document describes only the public HTTP surface, which the repo and
    // /llms-full.txt already document in prose — it exposes nothing new.
    builder.Services.AddOpenApi(options =>
    {
        options.AddSchemaTransformer<NumericSchemaTransformer>();
        options.AddSchemaTransformer<DiscriminatorRequiredTransformer>();
    });
    builder.Services.AddSignalR();
    builder.Services.AddSingleton(new PostgresNotificationOptions(connectionString));
    if (!generatingOpenApiDocument)
        builder.Services.AddHostedService<PostgresNotificationListener>();
    builder.Services.AddSingleton(CompilerSubmissionLimits.FromConfiguration(builder.Configuration));
    string networkHashKey = builder.Configuration["BOTARENA_NETWORK_HASH_KEY"]
        ?? (developmentAuth
            ? "nilbots-local-development-network-key"
            : throw new InvalidOperationException(
                "BOTARENA_NETWORK_HASH_KEY is required and must be a long random secret."));
    builder.Services.AddSingleton(new SubmissionNetwork(networkHashKey));
    builder.Services.AddScoped<ApplicationActorFactory>();
    builder.Services.AddScoped<CreateBotUseCase>();
    builder.Services.AddScoped<UpdateBotAppearanceUseCase>();
    builder.Services.AddScoped<BotStatisticsQuery>();
    builder.Services.AddScoped<CompilerSubmissionService>();
    builder.Services.AddScoped<SubmitBotVersionUseCase>();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(
                    System.Threading.RateLimiting.MetadataName.RetryAfter,
                    out TimeSpan retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            }
            return ValueTask.CompletedTask;
        };
        options.GlobalLimiter =
            System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
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
                $"{context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous"}:" +
                $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 6,
                    Window = TimeSpan.FromMinutes(10),
                }));
        options.AddPolicy("challenge", context =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.User.Identity?.Name ??
                context.Connection.RemoteIpAddress?.ToString() ??
                "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                }));
    });

    if (trustForwardedHeaders)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            // Safe only while production Kestrel binds to loopback or an exact
            // firewall-restricted private interface. Caddy is the sole public
            // route to every web replica.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }
}
else if (mode.RunsMigrations)
{
    // Migrations seed the OpenIddict client but do not need server certificates.
    builder.Services.AddBotArenaOpenIddict(
        builder.Configuration,
        includeServer: false,
        allowGeneratedCertificates: false,
        disableTransportSecurityRequirement: false);
}

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgres", tags: ["ready"])
    .AddCheck<ObjectStoreHealthCheck>("object-store", tags: ["ready"]);

if (mode.RunsAnyWorker)
    builder.Services.AddHostedService<JobWorker>();
if (mode.RunsCompilerRunner)
    builder.Services.AddHostedService<CompilerRunnerWorker>();

var app = builder.Build();
app.Logger.LogInformation("nilbots starting in {Role} role", mode.Name);

if (mode.RunsMigrations)
{
    await DatabaseBootstrapper.RunAsync(app.Services);
    app.Logger.LogInformation("Database migration and seed completed");
    return;
}

// `all` is the convenient local/pilot mode. Explicit production roles never
// mutate schema during normal startup.
if (mode.IsAll)
    await DatabaseBootstrapper.RunAsync(app.Services);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

if (mode.RunsWeb)
{
    if (trustForwardedHeaders)
        app.UseForwardedHeaders();
    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapOpenApi();
    app.MapAccounts();
    app.MapConnect();
    app.MapBots();
    app.MapCosmetics();
    app.MapMatches();
    app.MapRanked();
    app.MapUserNotifications();
    app.MapHub<UserNotificationsHub>("/hubs/notifications")
        .RequireAuthorization();

    app.MapGet("/api/meta", () =>
    {
        var maps = new List<MetaMapResponse>();
        if (RepoPaths.FindUpward("maps") is { } mapsDir)
        {
            foreach (var file in Directory.EnumerateFiles(mapsDir, "*.json").Order())
            {
                var map = ArenaMap.FromJson(File.ReadAllText(file));
                maps.Add(new MetaMapResponse(map.Id, map.Width, map.Height, map.ThemeId));
            }
        }
        return Results.Ok(new MetaResponse(
            BotArenaVersions.EngineVersion,
            BotArenaVersions.GameRulesVersion,
            BotArenaVersions.RuntimeProtocolVersion,
            ToolchainInfo.SdkVersion,
            // The axes a CLI must match to compile the same bytes this server will
            // (DECISIONS #93). SdkVersion + BuildPipelineVersion are what `submit`
            // gates on; CliVersion names the tool released alongside this server, so
            // the upgrade advice can be exact instead of "try updating".
            ToolchainInfo.BuildPipelineVersion,
            ToolchainInfo.CliVersion,
            maps));
    }).Produces<MetaResponse>();

    // Text mirrors for readers without a browser. The site is a JavaScript SPA, so
    // `curl /docs` yields a shell and scripted clients get nothing — and most of this
    // game's players are agents. Both routes stream the SAME markdown the repo ships,
    // so there is no second copy to drift (player-test round 2, top open finding).
    app.MapGet("/llms.txt", () =>
    {
        string origin = "https://nilbots.com";
        return Results.Text($"""
            # nilbots

            A programming game: you write a C# bot, it compiles to WebAssembly, and it
            fights other bots in a deterministic tile arena. Same engine locally and on
            the server; every match produces a verifiable replay.

            ## Start here (no browser required)
            dotnet tool install --global Nilbots
            nilbots register --email <you@example.com> --password <pw> --name <display>
            nilbots new MyBot
            cd MyBot
            nilbots play --bot . --opponent hunter --seeds 7,42,1337
            nilbots submit .
            nilbots leaderboard

            ## Full rules and API
            {origin}/llms-full.txt   the complete player guide for the current ruleset

            ## Reference
            nilbots --help        every command
            nilbots doctor        versions, toolchain, limits, sign-in state
            nilbots bots          training opponents and what each one does
            nilbots replay <file> --summary   per-tick state, vision, bolts, your debug lines

            ## What a bot may know
            Your own position, facing, health, cooldown; the tiles you can currently see;
            enemies inside that vision as (slot, position, facing, health); events you saw,
            and — under rules with hearing — coarse bearings for sounds you did not see.
            You do NOT learn who your opponent is, its cooldown, or anything outside your
            vision. Hidden information is the game: play the board, not the player.
            """, "text/plain; charset=utf-8");
    }).AllowAnonymous();

    app.MapGet("/llms-full.txt", () =>
    {
        string? guide = RepoPaths.FindUpward(Path.Combine("docs", "PLAYER-GUIDE.md"));
        return guide is null
            ? Results.NotFound("Player guide not found on this deployment.")
            : Results.Text(File.ReadAllText(guide), "text/plain; charset=utf-8");
    }).AllowAnonymous();

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
}

app.Run();

public partial class Program;
