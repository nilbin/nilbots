using BotArena.Toolchain;

namespace BotArena.Cli;

public static class NewCommand
{
    private const string DuelProfile = "duel";
    private const string GenericActorProfile = "generic-actor";

    public static int Run(string name) => Run(name, []);

    public static int Run(
        string name,
        IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(options, "profile");
        string profile = options
            .GetValueOrDefault("profile", DuelProfile)
            .ToLowerInvariant();
        string templateName = profile switch
        {
            DuelProfile => "botarena-bot",
            GenericActorProfile => "botarena-generic-actor",
            _ => throw new InvalidOperationException(
                $"Unknown bot profile '{profile}' " +
                $"(use {DuelProfile} or {GenericActorProfile})."),
        };

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                name,
                "^[A-Za-z][A-Za-z0-9]*$"))
        {
            Console.Error.WriteLine(
                "Bot name must be a valid C# identifier " +
                "(letters and digits, starting with a letter).");
            return 1;
        }
        // C# warns for all-lowercase type names because they may become reserved;
        // preserve the requested directory but emit a conventional entry type.
        string entryType = char.ToUpperInvariant(name[0]) + name[1..];
        string? templateDir = CliSupport.FindUpward(
            Path.Combine("templates", templateName));
        if (templateDir is null)
        {
            Console.Error.WriteLine(
                $"Template not found (templates/{templateName}).");
            return 1;
        }
        string targetDir = Path.GetFullPath(name);
        if (Directory.Exists(targetDir)
            && Directory.EnumerateFileSystemEntries(targetDir).Any())
        {
            Console.Error.WriteLine(
                $"Directory {targetDir} already exists and is not empty.");
            return 1;
        }
        Directory.CreateDirectory(targetDir);

        string? sdkProject = CliSupport.FindUpward(
            Path.Combine(
                "src",
                "BotArena.Sdk",
                "BotArena.Sdk.csproj"));
        string sdkReference;
        if (sdkProject is not null)
        {
            sdkReference =
                $"<ProjectReference Include=\"" +
                $"{Path.GetRelativePath(targetDir, sdkProject)}\" />";
        }
        else
        {
            string packagedSdk = Path.Combine(
                AppContext.BaseDirectory,
                "BotArena.Sdk.dll");
            if (!File.Exists(packagedSdk))
            {
                throw new InvalidOperationException(
                    "The installed CLI is missing BotArena.Sdk.dll; " +
                    "reinstall the Nilbots tool.");
            }
            string botLib = Path.Combine(targetDir, "lib");
            Directory.CreateDirectory(botLib);
            File.Copy(
                packagedSdk,
                Path.Combine(botLib, "BotArena.Sdk.dll"),
                overwrite: true);
            string packagedDocs = Path.ChangeExtension(packagedSdk, ".xml");
            if (File.Exists(packagedDocs))
            {
                File.Copy(
                    packagedDocs,
                    Path.Combine(botLib, "BotArena.Sdk.xml"),
                    overwrite: true);
            }
            sdkReference =
                "<Reference Include=\"BotArena.Sdk\">" +
                "<HintPath>lib/BotArena.Sdk.dll</HintPath></Reference>";
        }

        string rulesCard = profile == DuelProfile
            ? ReadCanonicalRulesCard()
            : "";
        foreach (string file in Directory.EnumerateFiles(templateDir))
        {
            string content = File.ReadAllText(file)
                .Replace("BOTNAME", entryType)
                .Replace("SDKVERSION", ToolchainInfo.SdkVersion)
                .Replace("<!--BOTARENA_RULES-->", rulesCard)
                .Replace(
                    "<!--BOTARENA_SDK_REFERENCE-->",
                    sdkReference);
            File.WriteAllText(
                Path.Combine(
                    targetDir,
                    Path.GetFileName(file)
                        .Replace("BOTNAME", entryType)),
                content);
        }
        Console.WriteLine($"Created bot project: {targetDir}");
        Console.WriteLine();
        Console.WriteLine($"  cd {name}");
        Console.WriteLine(
            profile == GenericActorProfile
                ? "  nilbots experiment frontline-labs --bot . " +
                  "--opponent <other-generic-bot> --seed 42"
                : "  nilbots play --bot . --opponent hunter --seed 42");
        if (sdkProject is not null)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Source checkout without `nilbots` on PATH:");
            string wrapper = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(sdkProject)!,
                    "..",
                    "..",
                    "scripts",
                    "botarena"));
            Console.WriteLine(
                profile == GenericActorProfile
                    ? $"  {wrapper} experiment frontline-labs --bot . " +
                      "--opponent <other-generic-bot> --seed 42"
                    : $"  {wrapper} play --bot . --opponent hunter " +
                      "--seed 42");
        }
        return 0;
    }

    /// <summary>
    /// The one copy of the player rules card. The same file backs
    /// /llms-full.txt, so the legacy scaffold and the site cannot disagree.
    /// </summary>
    private static string ReadCanonicalRulesCard()
    {
        const string guideName = "PLAYER-GUIDE.md";
        string packaged = Path.Combine(
            AppContext.BaseDirectory,
            "docs",
            guideName);
        string? path = File.Exists(packaged)
            ? packaged
            : CliSupport.FindUpward(Path.Combine("docs", guideName));
        if (path is null)
        {
            return "## Rules\n\nSee https://nilbots.com/llms-full.txt " +
                   "for the current rules.\n";
        }

        var lines = File.ReadAllLines(path).ToList();
        int start = lines.FindIndex(line =>
            line.StartsWith("## ", StringComparison.Ordinal));
        int end = lines.FindIndex(line =>
            line.StartsWith(
                "## Promotion evidence",
                StringComparison.Ordinal));
        if (start < 0)
            return string.Join('\n', lines);
        var body = lines.GetRange(
            start,
            (end > start ? end : lines.Count) - start);
        return "# Rules that decide matches\n\n" +
               "<!-- Spliced from the official rules card at " +
               "`nilbots new` time; the same\n" +
               "     text is served at " +
               "https://nilbots.com/llms-full.txt -->\n\n" +
               string.Join('\n', body).TrimEnd() + "\n";
    }
}
