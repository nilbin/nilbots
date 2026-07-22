using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>Stored CLI credentials (~/.botarena/credentials.json, 0600). Pilot stand-in
/// for the plan's §13.2 PKCE flow; tokens are minted in the web UI (My garage).</summary>
public sealed record Credentials(string Server, string Token)
{
    private static string PathFor() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".botarena", "credentials.json");

    public static Credentials? Load()
    {
        if (Environment.GetEnvironmentVariable("BOTARENA_TOKEN") is { Length: > 0 } token)
            return new Credentials(
                Environment.GetEnvironmentVariable("BOTARENA_SERVER") ?? "http://127.0.0.1:8080", token);
        string path = PathFor();
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<Credentials>(File.ReadAllText(path));
    }

    public void Save()
    {
        string path = PathFor();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static void Delete()
    {
        if (File.Exists(PathFor()))
            File.Delete(PathFor());
    }

    public HttpClient CreateClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(Server) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return client;
    }
}

public static class ServerCommands
{
    public static int Login(IReadOnlyList<string> args)
    {
        var options = CliSupport.ParseOptions(args);
        string server = options.GetValueOrDefault("server", "http://127.0.0.1:8080").TrimEnd('/');
        string? token = options.GetValueOrDefault("token");
        if (token is null)
        {
            Console.WriteLine($"Create a token in the web UI ({server}/garage → CLI access), then paste it:");
            token = Console.ReadLine()?.Trim();
        }
        if (string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("No token provided.");
            return 1;
        }
        var credentials = new Credentials(server, token);
        using var client = credentials.CreateClient();
        var me = client.GetFromJsonAsync<JsonElement>("/api/accounts/me").GetAwaiter().GetResult();
        credentials.Save();
        Console.WriteLine($"Signed in to {server} as {me.GetProperty("displayName").GetString()}.");
        return 0;
    }

    public static int Logout()
    {
        Credentials.Delete();
        Console.WriteLine("Signed out.");
        return 0;
    }

    public static int WhoAmI()
    {
        var credentials = Credentials.Load();
        if (credentials is null)
        {
            Console.WriteLine("Not signed in — run: botarena login");
            return 1;
        }
        using var client = credentials.CreateClient();
        var me = client.GetFromJsonAsync<JsonElement>("/api/accounts/me").GetAwaiter().GetResult();
        Console.WriteLine($"{me.GetProperty("displayName").GetString()} ({me.GetProperty("email").GetString()}) on {credentials.Server}");
        return 0;
    }

    /// <summary>Submit the bot project: local build first, then upload source for the
    /// canonical server build, then report local/server artifact parity (plan §11.3, §46).</summary>
    public static int Submit(IReadOnlyList<string> args)
    {
        var (directory, _) = BuildCommand.TakeDirectory(args);
        var credentials = Credentials.Load();
        if (credentials is null)
        {
            Console.Error.WriteLine("Not signed in — run: botarena login");
            return 1;
        }
        var project = BotProject.Load(directory);
        Console.WriteLine($"Building {project.Manifest.Name} locally...");
        var local = BotBuilder.EnsureBuilt(project);
        Console.WriteLine($"Local artifact:   {local.ArtifactHash} ({(local.FromCache ? "cache" : "compiled")})");

        using var client = credentials.CreateClient();
        string slug = BotSlug(project.Manifest.Name);
        var bots = client.GetFromJsonAsync<JsonElement>("/api/bots").GetAwaiter().GetResult();
        string? botId = bots.EnumerateArray()
            .Where(b => b.GetProperty("slug").GetString() == slug)
            .Select(b => b.GetProperty("id").GetString())
            .FirstOrDefault();
        if (botId is null)
        {
            var created = client.PostAsJsonAsync("/api/bots", new
            {
                name = project.Manifest.Name,
                accent = project.Accent,
            }).GetAwaiter().GetResult();
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
        var submitted = client.PostAsJsonAsync($"/api/bots/{botId}/versions", new
        {
            entryType = project.Manifest.EntryType,
            files,
        }).GetAwaiter().GetResult();
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
                Console.WriteLine(serverHash == local.ArtifactHash
                    ? "Parity:           IDENTICAL — local and server toolchains agree."
                    : "Parity:           DIFFERENT — likely toolchain/sysroot drift; the server " +
                      "artifact is canonical (see docs/DECISIONS.md #5).");
                Console.WriteLine($"Version {versionNumber} is now the active version. Fight: " +
                    $"{credentials.Server}/bots/{botId}");
                return 0;
            }
            Console.WriteLine($"  ...{status.ToLowerInvariant()}");
        }
    }

    private static string BotSlug(string name)
    {
        var builder = new System.Text.StringBuilder();
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
