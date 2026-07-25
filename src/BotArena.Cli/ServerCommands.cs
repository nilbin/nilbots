using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// CLI credentials from the OpenIddict Authorization Code + PKCE flow (plan §13.2).
/// Refresh credentials go to the OS secret service when available (Linux
/// `secret-tool`); otherwise a 0600 file with a warning.
/// </summary>
public sealed record StoredCredentials(
    string Server, string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    private const string SecretService = "botarena-cli";

    private static string FilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".botarena", "credentials.json");

    public static StoredCredentials? Load()
    {
        string? json = SecretToolLookup() ??
            (File.Exists(FilePath()) ? File.ReadAllText(FilePath()) : null);
        return json is null ? null : JsonSerializer.Deserialize<StoredCredentials>(json);
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(this);
        if (SecretToolStore(json))
            return;
        string path = FilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Console.Error.WriteLine(
            "note: no OS secret service available — credentials stored in ~/.botarena (0600).");
    }

    public static void Delete()
    {
        TryRun("secret-tool", $"clear service {SecretService}", null);
        if (File.Exists(FilePath()))
            File.Delete(FilePath());
    }

    private static string? SecretToolLookup() =>
        TryRun("secret-tool", $"lookup service {SecretService}", null);

    private static bool SecretToolStore(string secret) =>
        TryRun("secret-tool", $"store --label \"nilbots CLI\" service {SecretService}", secret) is not null;

    private static string? TryRun(string file, string arguments, string? stdin)
    {
        try
        {
            var info = new ProcessStartInfo(file, arguments)
            {
                RedirectStandardInput = stdin is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(info);
            if (process is null)
                return null;
            if (stdin is not null)
            {
                process.StandardInput.Write(stdin);
                process.StandardInput.Close();
            }
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? (stdin is not null ? "" : output.TrimEnd('\n')) : null;
        }
        catch
        {
            return null;
        }
    }
}

public static class ServerCommands
{
    private static readonly int[] CallbackPorts = [43117, 43118, 43119, 43120];
    private const string DefaultServer = "https://nilbots.com";

    public static int Register(IReadOnlyList<string> args) => Authenticate(args, register: true);

    public static int Login(IReadOnlyList<string> args) => Authenticate(args, register: false);

    private static int Authenticate(IReadOnlyList<string> args, bool register)
    {
        var options = CliSupport.ParseOptions(args);
        string server = options.GetValueOrDefault("server", DefaultServer).TrimEnd('/');

        // RFC 7636 PKCE material.
        string verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state = Base64Url(RandomNumberGenerator.GetBytes(16));

        // Headless: no browser anywhere (CI, a container, an autonomous agent).
        // Same Authorization Code + PKCE grant, driven over HTTP by the CLI itself —
        // the API's cookie session satisfies /connect/authorize, which then hands back
        // the code on its redirect. No new grant type, no weaker flow.
        if (options.ContainsKey("email") || options.ContainsKey("password"))
            return AuthenticateHeadless(options, server, register, verifier, challenge, state);

        using var listener = new HttpListener();
        int port = 0;
        foreach (int candidate in CallbackPorts)
        {
            try
            {
                listener.Prefixes.Clear();
                listener.Prefixes.Add($"http://127.0.0.1:{candidate}/callback/");
                listener.Start();
                port = candidate;
                break;
            }
            catch (HttpListenerException)
            {
            }
        }
        if (port == 0)
        {
            Console.Error.WriteLine("No loopback callback port available (43117-43120).");
            return 1;
        }

        string redirect = $"http://127.0.0.1:{port}/callback/";
        string authorizePath = "/connect/authorize" +
            "?client_id=botarena-cli&response_type=code&scope=offline_access" +
            $"&redirect_uri={Uri.EscapeDataString(redirect)}" +
            $"&state={state}&code_challenge={challenge}&code_challenge_method=S256";
        string url = register
            ? $"{server}/login?mode=register&returnUrl={Uri.EscapeDataString(authorizePath)}"
            : server + authorizePath;

        // stderr, and flushed: piping is the DEFAULT for agents and CI, and buffered
        // stdout meant this guidance never arrived — the command simply printed nothing
        // and blocked for five minutes (player-test S1-1, the worst moment in onboarding).
        Console.Error.WriteLine(register
            ? "Opening your browser to create an account. If nothing opens, visit:"
            : "Opening your browser to sign in. If nothing opens, visit:");
        Console.Error.WriteLine($"  {url}");
        Console.Error.WriteLine(
            "No browser at all? Cancel this and sign in without one:" +
            (register
                ? $"\n  nilbots register --email <you@example.com> --password <pw> [--name <display>] --server {server}"
                : $"\n  nilbots login --email <you@example.com> --password <pw> --server {server}"));
        Console.Error.Flush();
        TryOpenBrowser(url);

        var contextTask = listener.GetContextAsync();
        if (!contextTask.Wait(TimeSpan.FromMinutes(5)))
        {
            Console.Error.WriteLine("Timed out waiting for browser authentication.");
            return 1;
        }
        var context = contextTask.Result;
        string? code = context.Request.QueryString["code"];
        string? returnedState = context.Request.QueryString["state"];
        byte[] page = Encoding.UTF8.GetBytes(
            "<html><body style=\"background:#0a0e14;color:#c9d5e3;font-family:monospace;" +
            "display:flex;align-items:center;justify-content:center;height:100vh\">" +
            "Signed in — you can close this tab and return to the terminal.</body></html>");
        context.Response.ContentType = "text/html";
        context.Response.OutputStream.Write(page);
        context.Response.Close();
        listener.Stop();

        if (code is null || returnedState != state)
        {
            Console.Error.WriteLine("Authentication failed: missing code or state mismatch.");
            return 1;
        }

        using var http = new HttpClient();
        var tokens = http.PostAsync($"{server}/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "botarena-cli",
            ["code"] = code,
            ["redirect_uri"] = redirect,
            ["code_verifier"] = verifier,
        })).GetAwaiter().GetResult();
        if (!tokens.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Token exchange failed: {tokens.Content.ReadAsStringAsync().Result}");
            return 1;
        }
        SaveTokens(server, tokens.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult());

        using var client = CreateClient()!;
        var me = client.GetFromJsonAsync<JsonElement>("/api/accounts/me").GetAwaiter().GetResult();
        Console.WriteLine($"{(register ? "Account created; signed" : "Signed")} in to {server} " +
            $"as {me.GetProperty("displayName").GetString()}.");
        return 0;
    }

    public static int Logout()
    {
        StoredCredentials.Delete();
        Console.WriteLine("Signed out.");
        return 0;
    }

    public static int WhoAmI()
    {
        using var client = CreateClient();
        if (client is null)
            return NotSignedIn();
        string server = StoredCredentials.Load()!.Server;
        try
        {
            var me = client.GetFromJsonAsync<JsonElement>("/api/accounts/me").GetAwaiter().GetResult();
            Console.WriteLine($"{me.GetProperty("displayName").GetString()} " +
                $"({me.GetProperty("email").GetString()}) on {server}");
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Stored credentials outlive the server they were issued by (a local
            // dev server, a moved host). Report the identity we DO know and what to
            // do about it — never abort with a stack trace.
            Console.Error.WriteLine($"error: signed in against {server}, but it is unreachable ({ex.Message.TrimEnd('.')}).");
            Console.Error.WriteLine($"Run `nilbots login --server <url>` to sign in elsewhere, or `nilbots logout` to clear this.");
            return 1;
        }
    }

    /// <summary>Submit the bot project: local build first, then upload source for the
    /// canonical server build, then report local/server artifact parity (plan §11.3, §46).</summary>
    public static int Submit(IReadOnlyList<string> args)
    {
        var (directory, _) = BuildCommand.TakeDirectory(args);
        using var client = CreateClient();
        if (client is null)
            return NotSignedIn();
        string server = StoredCredentials.Load()!.Server;

        var project = BotProject.Load(directory);
        Console.WriteLine($"Building {project.Manifest.Name} locally...");
        var local = BotBuilder.EnsureBuilt(project);
        Console.WriteLine($"Local artifact:   {local.ArtifactHash} ({(local.FromCache ? "cache" : "compiled")})");

        string slug = BotsEndpointSlug(project.Manifest.Name);
        var bots = client.GetFromJsonAsync<JsonElement>("/api/bots").GetAwaiter().GetResult();
        string? botId = bots.EnumerateArray()
            .Where(b => b.GetProperty("slug").GetString() == slug)
            .Select(b => b.GetProperty("id").GetString())
            .FirstOrDefault();
        if (botId is null)
        {
            var created = client.PostAsJsonAsync("/api/bots",
                new { name = project.Manifest.Name, accent = project.Accent }).GetAwaiter().GetResult();
            if (!created.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Could not create bot: {created.Content.ReadAsStringAsync().Result}");
                return 1;
            }
            botId = created.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult()
                .GetProperty("id").GetString();
            Console.WriteLine($"Registered new bot '{project.Manifest.Name}'.");
        }

        var files = project.SourceFiles.Select(f => new
        {
            name = Path.GetRelativePath(project.Directory, f),
            content = File.ReadAllText(f),
        }).ToArray();
        var submitted = client.PostAsJsonAsync($"/api/bots/{botId}/versions",
            new { entryType = project.Manifest.EntryType, files }).GetAwaiter().GetResult();
        if (!submitted.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Submission rejected: {submitted.Content.ReadAsStringAsync().Result}");
            return 1;
        }
        int versionNumber = submitted.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult()
            .GetProperty("versionNumber").GetInt32();
        Console.WriteLine($"Submitted as version {versionNumber}; server build running...");

        while (true)
        {
            Thread.Sleep(2500);
            var bot = client.GetFromJsonAsync<JsonElement>($"/api/bots/{botId}").GetAwaiter().GetResult();
            var version = bot.GetProperty("versions").EnumerateArray()
                .Single(v => v.GetProperty("versionNumber").GetInt32() == versionNumber);
            string status = version.GetProperty("status").GetString()!;
            if (status == "Failed")
            {
                Console.Error.WriteLine("Server build FAILED. Build log:");
                Console.Error.WriteLine(version.GetProperty("buildLog").GetString());
                return 1;
            }
            if (status == "Built")
            {
                string serverHash = version.GetProperty("artifactHash").GetString()!;
                Console.WriteLine($"Server artifact:  {serverHash}");
                // Builds are reproducible across build directories since
                // BuildPipelineVersion 2 (DECISIONS #81), so a mismatch is no longer the
                // expected outcome and must not be hand-waved as "drift" — it means the
                // two sides genuinely differ (different CLI/SDK version, or a real
                // toolchain difference). Say so, and say which one counts.
                if (serverHash == local.ArtifactHash)
                    Console.WriteLine("Parity:           IDENTICAL — same sources, same bytes.");
                else
                {
                    // Never cite repo-only files at players, and never guess "drift":
                    // the usual cause is a version gap, which we can actually check for
                    // them (player-test S1-3).
                    Console.WriteLine("Parity:           DIFFERENT — your CLI and this server built different bytes.");
                    string? serverSdk = TryGetServerSdkVersion(client);
                    Console.WriteLine(serverSdk is not null && serverSdk != ToolchainInfo.SdkVersion
                        ? $"                  Your CLI bundles SDK {ToolchainInfo.SdkVersion}; this server builds with SDK {serverSdk}. " +
                          "Upgrade with: dotnet tool update -g Nilbots"
                        : $"                  Your CLI bundles SDK {ToolchainInfo.SdkVersion}. Check `nilbots --version` " +
                          "against the server's /api/meta for a version gap.");
                    Console.WriteLine("                  The SERVER artifact is the one that plays, so test against it.");
                }
                Console.WriteLine($"Version {versionNumber} is now the active version. Fight: {server}/bots/{botId}");
                return 0;
            }
            Console.WriteLine($"  ...{status.ToLowerInvariant()}");
        }
    }

    /// <summary>Authenticated client; transparently refreshes an expired access token.</summary>
    private static HttpClient? CreateClient()
    {
        var credentials = StoredCredentials.Load();
        if (credentials is null)
            return null;
        if (credentials.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            using var http = new HttpClient();
            HttpResponseMessage refreshed;
            try
            {
                refreshed = http.PostAsync($"{credentials.Server}/connect/token",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["client_id"] = "botarena-cli",
                        ["refresh_token"] = credentials.RefreshToken,
                    })).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Credentials outlive the server that issued them (a local dev server,
                // a moved host). This refresh runs before any command's own error
                // handling, so without this EVERY authenticated command — whoami,
                // submit — aborted here with a stack trace.
                Console.Error.WriteLine(
                    $"error: cannot reach {credentials.Server} to refresh your session " +
                    $"({ex.Message.TrimEnd('.')}).");
                Console.Error.WriteLine(
                    "Run `nilbots login --server <url>` to sign in elsewhere, or `nilbots logout` to clear it.");
                return null;
            }
            if (!refreshed.IsSuccessStatusCode)
            {
                Console.Error.WriteLine("Session expired — run: nilbots login");
                return null;
            }
            SaveTokens(credentials.Server,
                refreshed.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult());
            credentials = StoredCredentials.Load()!;
        }
        var client = new HttpClient { BaseAddress = new Uri(credentials.Server) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        return client;
    }

    private static void SaveTokens(string server, JsonElement response)
    {
        // OpenIddict rotates refresh tokens; keep the old one only if none is returned.
        string refresh = response.TryGetProperty("refresh_token", out var r)
            ? r.GetString()!
            : StoredCredentials.Load()?.RefreshToken ?? "";
        new StoredCredentials(
            server,
            response.GetProperty("access_token").GetString()!,
            refresh,
            DateTimeOffset.UtcNow.AddSeconds(response.GetProperty("expires_in").GetInt32())).Save();
    }

    /// <summary>Exit path when <see cref="CreateClient"/> yields no client. Stored
    /// credentials that failed to refresh have ALREADY explained themselves (expired,
    /// or server unreachable); telling that user "not signed in" is both wrong and
    /// contradictory, so only a genuinely empty credential store prints it.</summary>
    /// <summary>Browser-free Authorization Code + PKCE. The API's cookie session is
    /// what /connect/authorize checks, so once we hold that cookie the authorize
    /// endpoint answers with a redirect carrying the code — we read it off the
    /// Location header instead of listening on loopback. Identical grant, identical
    /// tokens; the only thing removed is the browser.</summary>
    private static int AuthenticateHeadless(
        Dictionary<string, string> options, string server, bool register,
        string verifier, string challenge, string state)
    {
        if (!options.TryGetValue("email", out string? email)
            || !options.TryGetValue("password", out string? password))
        {
            Console.Error.WriteLine("error: --email and --password are both required for headless sign-in.");
            Console.Error.WriteLine(register
                ? "Usage: nilbots register --email <a@b.c> --password <pw> [--name <display>] [--server <url>]"
                : "Usage: nilbots login --email <a@b.c> --password <pw> [--server <url>]");
            return 1;
        }
        string displayName = options.GetValueOrDefault("name", email.Split('@')[0]);

        using var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = false, // we need to READ the redirect, not follow it
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(server) };

        string path = register ? "/api/accounts/register" : "/api/accounts/login";
        object payload = register
            ? new { displayName, email, password }
            : (object)new { email, password };
        var account = http.PostAsJsonAsync(path, payload).GetAwaiter().GetResult();
        if (!account.IsSuccessStatusCode)
        {
            string body = account.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Console.Error.WriteLine(
                $"error: {(register ? "registration" : "sign-in")} failed ({(int)account.StatusCode}): {Truncate(body, 300)}");
            if (register && account.StatusCode == HttpStatusCode.Conflict)
                Console.Error.WriteLine("That email already exists — use `nilbots login --email ... --password ...`.");
            return 1;
        }
        Console.WriteLine(register ? $"Account created for {email}." : $"Signed in as {email}.");

        string redirect = $"http://127.0.0.1:{CallbackPorts[0]}/callback/";
        string authorize = "/connect/authorize" +
            "?client_id=botarena-cli&response_type=code&scope=offline_access" +
            $"&redirect_uri={Uri.EscapeDataString(redirect)}" +
            $"&state={state}&code_challenge={challenge}&code_challenge_method=S256";
        var authorized = http.GetAsync(authorize).GetAwaiter().GetResult();
        if (authorized.Headers.Location is null)
        {
            Console.Error.WriteLine(
                $"error: the authorization endpoint did not return a redirect ({(int)authorized.StatusCode}). " +
                "The session cookie may have been rejected.");
            return 1;
        }
        var query = System.Web.HttpUtility.ParseQueryString(authorized.Headers.Location.Query);
        string? code = query["code"];
        if (code is null || query["state"] != state)
        {
            Console.Error.WriteLine(
                $"error: authorization did not yield a code (state mismatch or denial): {authorized.Headers.Location}");
            return 1;
        }

        var tokens = http.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "botarena-cli",
            ["code"] = code,
            ["redirect_uri"] = redirect,
            ["code_verifier"] = verifier,
        })).GetAwaiter().GetResult();
        if (!tokens.IsSuccessStatusCode)
        {
            Console.Error.WriteLine(
                $"error: token exchange failed: {Truncate(tokens.Content.ReadAsStringAsync().GetAwaiter().GetResult(), 300)}");
            return 1;
        }
        SaveTokens(server, tokens.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult());
        Console.WriteLine($"Signed in to {server} — no browser needed. Next: nilbots new <Name>, then nilbots submit .");
        return 0;
    }

    /// <summary>Two lines for `doctor`: who we are signed in as, and whether the server
    /// builds with the same SDK this CLI bundles. A version gap is the usual reason a
    /// submission's artifact parity differs, so surfacing it here means the player is
    /// told BEFORE they submit rather than left guessing after.</summary>
    public static void ReportSessionHealth()
    {
        var credentials = StoredCredentials.Load();
        if (credentials is null)
        {
            Console.WriteLine($"{"Authentication:",-24}not signed in — run: nilbots register (or login)");
            Console.WriteLine($"{"Server compatibility:",-24}n/a (sign in first)");
            return;
        }
        Console.WriteLine($"{"Authentication:",-24}signed in to {credentials.Server}" +
            (credentials.ExpiresAt <= DateTimeOffset.UtcNow ? " (token expired — refreshes on next use)" : ""));
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(credentials.Server), Timeout = TimeSpan.FromSeconds(10) };
            var meta = http.GetFromJsonAsync<JsonElement>("/api/meta").GetAwaiter().GetResult();
            string? serverSdk = meta.TryGetProperty("sdkVersion", out var sdk) ? sdk.GetString() : null;
            string? serverRules = meta.TryGetProperty("gameRulesVersion", out var r) ? r.GetString() : null;
            Console.WriteLine($"{"Server compatibility:",-24}" + (serverSdk == ToolchainInfo.SdkVersion
                ? $"OK  server SDK {serverSdk}, rules {serverRules}"
                : $"SKEW — server builds with SDK {serverSdk}, this CLI bundles {ToolchainInfo.SdkVersion}. " +
                  $"Artifacts will differ; upgrade with: dotnet tool update -g Nilbots"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"{"Server compatibility:",-24}unknown — {credentials.Server} unreachable");
        }
    }

    /// <summary>The ladder, in the terminal. Ranked play is the headline activity and
    /// was reachable only by hand-written curl against /api/leaderboard, which no CLI
    /// help mentioned (player-test round 2). Anonymous: reading the ladder needs no
    /// account, so newcomers can look before they commit.</summary>
    public static int Leaderboard(IReadOnlyList<string> args)
    {
        var options = CliSupport.ParseOptions(args);
        string server = options.GetValueOrDefault("server",
            StoredCredentials.Load()?.Server ?? DefaultServer).TrimEnd('/');
        string query = options.TryGetValue("rules", out string? rules) ? $"?rules={Uri.EscapeDataString(rules)}" : "";
        using var http = new HttpClient { BaseAddress = new Uri(server) };
        var board = http.GetFromJsonAsync<JsonElement>($"/api/leaderboard{query}").GetAwaiter().GetResult();

        string version = board.GetProperty("rulesVersion").GetString() ?? "?";
        var entries = board.GetProperty("entries").EnumerateArray().ToList();
        Console.WriteLine($"Ladder for rules {version} ({server})");
        if (entries.Count == 0)
        {
            Console.WriteLine("  (nobody has played a ranked set yet — be the first: nilbots rank <bot> <opponent>)");
            return 0;
        }
        Console.WriteLine($"  {"#",-4}{"bot",-22}{"owner",-18}{"elo",6}  sets");
        int rank = 0;
        foreach (var entry in entries)
            Console.WriteLine($"  {++rank,-4}{entry.GetProperty("name").GetString(),-22}" +
                $"{entry.GetProperty("owner").GetString(),-18}" +
                $"{entry.GetProperty("rating").GetDouble(),6:0}  {entry.GetProperty("rankedSets").GetInt32()}");
        var ladders = board.GetProperty("ladders").EnumerateArray()
            .Select(l => l.GetString()).Where(l => l != version).ToList();
        if (ladders.Count > 0)
            Console.WriteLine($"  other ladders: {string.Join(", ", ladders)}  (see them with --rules <version>)");
        return 0;
    }

    /// <summary>Queue a ranked set against another bot on the ladder, by name.</summary>
    public static int Rank(IReadOnlyList<string> args)
    {
        var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        var options = CliSupport.ParseOptions(args.SkipWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList());
        using var client = CreateClient();
        if (client is null)
            return NotSignedIn();
        string server = StoredCredentials.Load()!.Server;

        var bots = client.GetFromJsonAsync<JsonElement>("/api/bots").GetAwaiter().GetResult()
            .EnumerateArray().ToList();
        string? Find(string name) => bots
            .Where(b => string.Equals(b.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
            .Select(b => b.GetProperty("id").GetString())
            .FirstOrDefault();

        if (positional.Count != 2)
        {
            Console.Error.WriteLine("Usage: nilbots rank <your-bot> <opponent>   (names as shown by `nilbots leaderboard`)");
            Console.Error.WriteLine($"Bots on {server}: {string.Join(", ", bots.Select(b => b.GetProperty("name").GetString()))}");
            return 1;
        }
        string? mine = Find(positional[0]), theirs = Find(positional[1]);
        if (mine is null || theirs is null)
        {
            Console.Error.WriteLine($"error: no bot named '{(mine is null ? positional[0] : positional[1])}' on {server}.");
            Console.Error.WriteLine("Run `nilbots leaderboard` to see who is playing.");
            return 1;
        }

        object body = options.TryGetValue("rules", out string? rules)
            ? new { botId = mine, opponentBotId = theirs, rules }
            : (object)new { botId = mine, opponentBotId = theirs };
        var response = client.PostAsJsonAsync("/api/matches/ranked", body).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine(
                $"error: ranked challenge refused ({(int)response.StatusCode}): " +
                $"{Truncate(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), 300)}");
            return 1;
        }
        var set = response.Content.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult();
        string setId = set.GetProperty("id").GetString()!;
        Console.WriteLine($"Ranked set queued: {positional[0]} vs {positional[1]}");
        Console.WriteLine($"  Watch: {server}/sets/{setId}");
        Console.WriteLine($"  Results are withheld until every game has broadcast — then: nilbots leaderboard");
        return 0;
    }

    /// <summary>The server's build SDK, from its public /api/meta. Best-effort: a
    /// parity report must never fail because the probe did.</summary>
    private static string? TryGetServerSdkVersion(HttpClient client)
    {
        try
        {
            var meta = client.GetFromJsonAsync<JsonElement>("/api/meta").GetAwaiter().GetResult();
            return meta.TryGetProperty("sdkVersion", out var sdk) ? sdk.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static int NotSignedIn()
    {
        if (StoredCredentials.Load() is null)
            Console.Error.WriteLine("Not signed in — run: nilbots register or nilbots login");
        return 1;
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch
        {
            // The URL is printed; the user can open it manually.
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string BotsEndpointSlug(string name)
    {
        var builder = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        return builder.ToString().Trim('-');
    }
}
