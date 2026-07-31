using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the engine's derived surfaces to the source of truth so they cannot rot
/// silently. Every drift this suite guards against has actually happened once:
/// web/types.ts missed energy/zoneTicks/Domination/strafes for two experiment
/// cycles; the site docs described a stale ranked pool and rules version; SDK
/// comments claimed strafe shipped in 0.3. Mechanical agreement is testable —
/// these read the repo files at test time; prose accuracy stays a review duty
/// (see the rules-change checklist in CLAUDE.md).
/// </summary>
public class DocDriftTests
{
    private static readonly string Root = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BotArena.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("BotArena.sln not found above the test directory.");
    }

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root, .. parts]));

    [Fact]
    public void TypeScriptReplayMirror_ContainsEveryEnumValue()
    {
        string ts = ReadRepoFile("web", "src", "types.ts");
        Type[] enums = [typeof(BotAction), typeof(ActionResult), typeof(BotStatus),
                        typeof(MatchEndReason), typeof(GameEventType)];
        foreach (var type in enums)
            foreach (string name in Enum.GetNames(type))
                Assert.True(ts.Contains($"'{name}'", StringComparison.Ordinal),
                    $"web/src/types.ts is missing {type.Name} value '{name}' — keep the replay mirror in sync with Replay.cs.");
    }

    [Fact]
    public void TypeScriptReplayMirror_ContainsEveryReplayProperty()
    {
        string ts = ReadRepoFile("web", "src", "types.ts");
        Type[] records = [typeof(ReplayHeader), typeof(ReplayParticipant), typeof(ReplayBotTick),
                          typeof(ReplayBotState), typeof(ReplayVisibleEnemy), typeof(GameEvent),
                          typeof(ReplayTick), typeof(ReplayProjectile), typeof(ReplayProjectileTraversal),
                          typeof(ReplayHeardSound),
                          typeof(MatchResultInfo), typeof(BotMatchResult)];
        foreach (var type in records)
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string camel = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
                Assert.True(
                    System.Text.RegularExpressions.Regex.IsMatch(ts, $@"\b{camel}\??:"),
                    $"web/src/types.ts is missing {type.Name}.{property.Name} (expected '{camel}:' or '{camel}?:').");
            }
    }

    [Fact]
    public void PlayerDocs_DescribeTheCurrentRulesVersion()
    {
        // Both player-facing docs are now assembled rather than hand-written: the site
        // renders the canonical card and stamps the version from a constant, and the
        // template README splices the card in at `nilbots new` time. So assert the
        // version reaches each surface, not that a literal "v0.5" is typed there.
        Assert.Contains($"RULES_VERSION = '{BotArenaVersions.GameRulesVersion}'",
            ReadRepoFile("web", "src", "site", "pages", "DocsPage.tsx"));
        Assert.Contains(BotArenaVersions.GameRulesVersion,
            ReadRepoFile("docs", "PLAYER-GUIDE.md"));
    }

    /// <summary>The app serves web/dist/index.html directly, so a stale bundle silently
    /// shows players the PREVIOUS ruleset's docs while the source reads correctly — a
    /// friend's-agent test was taught "150 zone-ticks wins" (a pre-0.5 mechanic) from a
    /// two-day-old bundle. dist is gitignored, so only assert when it exists.</summary>
    [Fact]
    public void BuiltWebBundle_IfPresent_IsNotStaleAgainstTheDocsSource()
    {
        string dist = Path.Combine(Root, "web", "dist", "index.html");
        string source = Path.Combine(Root, "web", "src", "site", "pages", "DocsPage.tsx");
        if (!File.Exists(dist))
            return; // nothing built here (fresh clone / CI before the web build)
        Assert.True(File.GetLastWriteTimeUtc(dist) >= File.GetLastWriteTimeUtc(source),
            "web/dist is older than the docs source, so the site would serve stale rules " +
            "to players. Rebuild it: (cd web && npm run build).");
    }

    /// <summary>The canonical player rules card is now the ONE source of rules prose:
    /// the server serves it at /llms-full.txt and `nilbots new` splices it into every
    /// scaffolded README. So its numbers must match the engine — a stale number here
    /// now reaches every player at once, which is exactly how "150 zone-ticks wins"
    /// outlived the mechanic it described.</summary>
    [Fact]
    public void CanonicalRulesCard_AgreesWithTheEngineNumbers()
    {
        string card = ReadRepoFile("docs", "PLAYER-GUIDE.md");
        var rules = GameRules.Current;
        (string Label, string Value)[] mustAppear =
        [
            ("vision range", rules.VisionRange.ToString()),
            ("shoot cooldown", rules.ShootCooldownTicks.ToString()),
            ("max health", rules.MaxHealth.ToString()),
            ("max ticks", rules.MaxTicks.ToString()),
        ];
        foreach (var (label, value) in mustAppear)
            Assert.True(card.Contains(value, StringComparison.Ordinal),
                $"The player rules card never mentions the engine's {label} ({value}). " +
                "Either the card is stale or the value moved — players read this verbatim.");

        // The retired pre-0.5 win condition must not survive anywhere in it.
        Assert.False(card.Contains("zone-ticks wins", StringComparison.OrdinalIgnoreCase),
            "The rules card still describes the pre-0.5 zone-tick win condition.");
    }

    [Fact]
    public void CliHelp_ListsEveryResolvableRulesName()
    {
        string help = ReadRepoFile("src", "BotArena.Cli", "Program.cs");
        foreach (string name in GameRules.KnownNames)
            Assert.True(help.Contains(name, StringComparison.Ordinal),
                $"CLI help in Program.cs does not mention rules name '{name}' (GameRules.KnownNames).");
    }

    [Fact]
    public void CliDefault_FollowsTheCurrentRulesVersion()
    {
        string support = ReadRepoFile("src", "BotArena.Cli", "CliSupport.cs");
        Assert.Contains("return BotArenaVersions.GameRulesVersion;", support);
    }

    [Fact]
    public void EveryKnownRulesName_Resolves()
    {
        foreach (string name in GameRules.KnownNames)
            _ = GameRules.Resolve(name); // must not throw; KnownNames and Resolve move together
    }

    /// <summary>The class-skill brief is the only document an agent-arena author gets for
    /// the kit, so its mechanical lists have to be the engine's. Every stance form ID and
    /// every arm token is derived here rather than typed, which is what caught the arm
    /// rename: changing a token without updating the brief now fails the build.</summary>
    [Fact]
    public void ClassSkillsBrief_NamesEveryStanceFormAndArmToken()
    {
        string brief = ReadRepoFile("docs", "EXPERIMENTAL-FRONTLINE-CLASSES.md");
        foreach (var entry in FrontlineLabsClassDefinition.All)
        {
            if (entry.Skill is not (FrontlineLabsSkillKit.StrikerVolley
                or FrontlineLabsSkillKit.BulwarkAegisShell))
                continue;
            foreach (string formId in new[] { entry.PrimeStanceFormId, entry.ChildStanceFormId })
                Assert.True(brief.Contains(formId, StringComparison.Ordinal),
                    $"The class-skills brief never names the stance form '{formId}'.");
        }

        // The arm identities the brief quotes must be the ones the engine mints.
        foreach (string armId in new[]
                 {
                     FrontlineLabsDefinition.CreatePendulumExperiment(
                         FrontlineLabsPendulumArm.None,
                         (FrontlineLabsClassDefinition.Bulwark, FrontlineLabsClassDefinition.Striker),
                         skills: FrontlineLabsSkillKit.StrikerVolley
                             | FrontlineLabsSkillKit.BulwarkAegisShell).Rules.RulesetId,
                 })
            Assert.True(brief.Contains(armId, StringComparison.Ordinal),
                $"The class-skills brief quotes no arm identity matching '{armId}'.");
    }

    /// <summary>The automatic return is a rule players plan against, so the brief must state
    /// its canonical spellings — the counters, the reason code, and both budgets — in the
    /// exact strings the contract and replay carry.</summary>
    [Fact]
    public void ClassSkillsBrief_StatesTheAutomaticReturnContractVerbatim()
    {
        string brief = ReadRepoFile("docs", "EXPERIMENTAL-FRONTLINE-CLASSES.md");
        var counters = Enum.GetValues<ActorAutomaticReturnTriggerDefinition.AutomaticReturnCounterKind>();
        foreach (var counter in counters)
        {
            string id = ActorContractCanonicalIds.Id(counter);
            Assert.True(brief.Contains(id, StringComparison.Ordinal),
                $"The class-skills brief never names the automatic-return counter '{id}'.");
        }
        Assert.Contains("automaticReturn", brief, StringComparison.Ordinal);
        Assert.Contains("automatic-threshold-return", brief, StringComparison.Ordinal);
        Assert.Contains(
            $"threshold **{FrontlineLabsDefinition.VolleyCastBudget}**", brief, StringComparison.Ordinal);
        Assert.Contains(
            $"threshold **{FrontlineLabsDefinition.ShieldBreakBudget}**", brief, StringComparison.Ordinal);
    }

    /// <summary>The bend envelope varies by class, and a stale number here would teach every
    /// author to submit programs the engine rejects.</summary>
    [Fact]
    public void ClassSkillsBrief_QuotesEachClassBendDepth()
    {
        string brief = ReadRepoFile("docs", "EXPERIMENTAL-FRONTLINE-CLASSES.md");
        foreach (var entry in FrontlineLabsClassDefinition.All)
            Assert.True(
                brief.Contains($"1–{entry.MobileMaxBendAfterTiles}", StringComparison.Ordinal)
                || brief.Contains($"1-{entry.MobileMaxBendAfterTiles}", StringComparison.Ordinal),
                $"The class-skills brief never states {entry.Id}'s bend depth "
                + $"(1-{entry.MobileMaxBendAfterTiles} tiles).");
    }

    [Fact]
    public void CliHelp_ListsEveryBendEnvelopeAndSkillArm()
    {
        string help = ReadRepoFile("src", "BotArena.Cli", "Program.cs");
        foreach (string token in new[] { "striker-only", "universal" })
            Assert.True(help.Contains(token, StringComparison.Ordinal),
                $"CLI help does not mention --bend '{token}'.");
    }

    [Fact]
    public void SdkVersionNote_NamesTheMindApiItShipped()
    {
        // The version notes are how a frozen artifact's capabilities are read
        // back years later. A version that moved because a whole programming
        // model landed has to say so, by name, or the note is worse than
        // absent.
        string toolchain = ReadRepoFile("src", "BotArena.Toolchain", "BotProject.cs");
        foreach (string token in new[]
                 {
                     "IGenericMindBot",
                     "MindContext",
                     "MindBody",
                     "generic-mind-match-1",
                     "WrappedPerLifeMind",
                 })
        {
            Assert.True(
                toolchain.Contains(token, StringComparison.Ordinal),
                $"The SDK version notes never mention '{token}'.");
        }
    }

    [Fact]
    public void QualificationBrief_NamesEveryMindSuiteAndProfileItMinted()
    {
        // A suite ID is a pre-registration, and an unwritten one is a suite
        // nobody can ask for. These are mechanical mirrors of engine
        // constants, which is exactly the class of drift this file pins.
        string brief = ReadRepoFile("docs", "BOT-QUALIFICATION-SUITE.md");
        foreach (string token in new[]
                 {
                     FrontlineLabsQualificationDefinition
                         .MindFundamentalsSuiteId,
                     FrontlineLabsQualificationDefinition
                         .MindFundamentalsProfileId,
                     FrontlineLabsQualificationDefinition
                         .MindTacticalSuiteId,
                     FrontlineLabsQualificationDefinition
                         .MindTacticalProfileId,
                     FrontlineLabsQualificationDefinition
                         .MindPositionalSuiteId,
                     FrontlineLabsQualificationDefinition
                         .MindPositionalProfileId,
                     FrontlineLabsQualificationDefinition.BodyHandoffProbeId,
                     FrontlineLabsQualificationDefinition
                         .EscortIntegrityProbeId,
                 })
        {
            Assert.True(
                brief.Contains(token, StringComparison.Ordinal),
                $"BOT-QUALIFICATION-SUITE.md never mentions '{token}'.");
        }
    }

    [Fact]
    public void CliHelp_ReachesTheMindProfileFromEveryCommandThatTakesIt()
    {
        // The mind is only worth building if a player can get to it, and the
        // help text is the whole discovery surface for a local-only profile
        // that no hosted lane advertises.
        string help = ReadRepoFile("src", "BotArena.Cli", "Program.cs");
        foreach (string token in new[]
                 {
                     "generic-mind",
                     "--profile mind",
                     FrontlineLabsQualificationDefinition
                         .MindFundamentalsSuiteId,
                     "IGenericMindBot",
                 })
        {
            Assert.True(
                help.Contains(token, StringComparison.Ordinal),
                $"CLI help never mentions '{token}'.");
        }
    }

    [Fact]
    public void RulesCard_StatesTheMindMemoryModelAndItsFaultCost()
    {
        // Every clause of the per-life memory paragraph inverts under the
        // mind, and the one that costs a match — a trap forgets everything —
        // has to be loud rather than inferable.
        string rules = ReadRepoFile("docs", "FRONTLINE-LABS-RULES.md");
        foreach (string token in new[]
                 {
                     BotArenaVersions.GenericMindContractProfileId,
                     "forgets the match",
                     "SetRole",
                 })
        {
            Assert.True(
                rules.Contains(token, StringComparison.Ordinal),
                $"FRONTLINE-LABS-RULES.md never mentions '{token}'.");
        }
    }

    /// <summary>
    /// The roster arm has TWO spellings — the flag the CLI accepts (`legion`)
    /// and the ruleset-ID token the engine appends (`levy`) — and a whole
    /// section of the brief was written against the wrong one, telling authors
    /// to pass a flag that does not parse and naming a map generation that does
    /// not exist. Both facts are mechanical, so pin both.
    /// </summary>
    [Fact]
    public void ClassSkillsBrief_SpellsTheRosterFlagAndItsMapGeneration()
    {
        string brief = ReadRepoFile("docs", "EXPERIMENTAL-FRONTLINE-CLASSES.md");
        Assert.Contains("`--roster legion`", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("`--roster levy`", brief, StringComparison.Ordinal);
        Assert.Contains(
            FrontlineLabsLegionRoster.MapId,
            brief,
            StringComparison.Ordinal);
        // The tranche ticks the same section tabulates.
        foreach (int tick in new[]
                 {
                     FrontlineLabsLegionRoster.MidTrancheUnlockTick,
                     FrontlineLabsLegionRoster.LateTrancheUnlockTick,
                 })
        {
            Assert.True(
                brief.Contains(
                    tick.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal),
                $"The class-skills brief never states the tranche tick {tick}.");
        }
    }

    /// <summary>
    /// `--print-candidate-contract` grew a mode after three waves of authors
    /// mined replay headers for declared numbers. Help that does not name the
    /// mode is help that keeps them mining.
    /// </summary>
    [Fact]
    public void CliHelp_NamesEveryCandidateContractPrintMode()
    {
        string help = ReadRepoFile("src", "BotArena.Cli", "Program.cs");
        foreach (string token in new[] { "identity", "full" })
        {
            Assert.True(
                help.Contains(
                    $"--print-candidate-contract [{token}",
                    StringComparison.Ordinal)
                || help.Contains(
                    $"|{token}]",
                    StringComparison.Ordinal),
                $"CLI help does not offer --print-candidate-contract '{token}'.");
        }
    }

    /// <summary>
    /// The abort exit code exists so a sweep can tell "this cell measured
    /// nothing" from "your flags were wrong". A code nobody documents is a
    /// code nobody wires in.
    /// </summary>
    [Fact]
    public void AuthorPacketAndCliHelp_StateTheAbortExitCode()
    {
        string help = ReadRepoFile("src", "BotArena.Cli", "Program.cs");
        Assert.Contains("ABORTED", help, StringComparison.Ordinal);
        string packet = ReadRepoFile(
            "docs",
            "FRONTLINE-LABS-BOT-AUTHOR-PACKET.md");
        Assert.Contains("never score it", packet, StringComparison.Ordinal);
    }

    /// <summary>
    /// Wave-8 friction: the salvo section told authors to read an enemy's fan
    /// entry off `routeCooldowns`. The observation type is the authority, and
    /// it publishes the field on SELF and ALLIES only — so the pin is the type,
    /// and the brief must say which.
    /// </summary>
    [Fact]
    public void ClassSkillsBrief_DoesNotClaimEnemiesPublishRouteCooldowns()
    {
        const string field = "RouteCooldowns";
        Assert.NotNull(
            typeof(GenericActorRuntimeObservation.ObservedSelfState)
                .GetProperty(field));
        Assert.NotNull(
            typeof(GenericActorRuntimeObservation.ObservedAllyState)
                .GetProperty(field));
        Assert.Null(
            typeof(GenericActorRuntimeObservation.ObservedEnemyState)
                .GetProperty(field));

        string brief = ReadRepoFile("docs", "EXPERIMENTAL-FRONTLINE-CLASSES.md");
        Assert.Contains("never on an enemy", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void SdkProjectVersion_MatchesToolchainVersion()
    {
        string toolchain = ReadRepoFile("src", "BotArena.Toolchain", "BotProject.cs");
        var versionMatch = Regex.Match(
            toolchain,
            """public const string SdkVersion = "([^"]+)";""");
        Assert.True(versionMatch.Success, "Could not find ToolchainInfo.SdkVersion.");

        var project = XDocument.Parse(
            ReadRepoFile("src", "BotArena.Sdk", "BotArena.Sdk.csproj"));
        string? projectVersion = project.Descendants("Version").SingleOrDefault()?.Value;
        Assert.Equal(versionMatch.Groups[1].Value, projectVersion);
    }
}
