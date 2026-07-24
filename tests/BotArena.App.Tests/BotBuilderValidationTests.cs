using BotArena.Toolchain;

namespace BotArena.App.Tests;

public class BotBuilderValidationTests
{
    [Theory]
    [InlineData("../Escape.cs")]
    [InlineData("nested\\Windows.cs")]
    [InlineData("line\nbreak.cs")]
    [InlineData("__BotArenaMain.cs")]
    public void ComputeCacheKey_RejectsUnsafeSourcePaths(string path)
    {
        Assert.Throws<BotBuildException>(
            () => BotBuilder.ComputeCacheKey([new SourceFile(path, "class Bot {}")], "Bot"));
    }

    [Fact]
    public void ComputeCacheKey_RejectsDuplicatePathsCaseInsensitively()
    {
        Assert.Throws<BotBuildException>(() => BotBuilder.ComputeCacheKey(
            [
                new SourceFile("Bot.cs", "class Bot {}"),
                new SourceFile("bot.cs", "class Other {}"),
            ],
            "Bot"));
    }

    [Fact]
    public void ComputeCacheKey_RejectsTooManyOrTooLargeSources()
    {
        SourceFile[] tooMany = Enumerable.Range(0, BotBuilder.MaxSourceFiles + 1)
            .Select(index => new SourceFile($"Bot{index}.cs", "class Bot {}"))
            .ToArray();
        Assert.Throws<BotBuildException>(() => BotBuilder.ComputeCacheKey(tooMany, "Bot"));
        Assert.Throws<BotBuildException>(() => BotBuilder.ComputeCacheKey(
            [new SourceFile("Bot.cs", new string('x', BotBuilder.MaxTotalSourceBytes + 1))],
            "Bot"));
    }

    [Fact]
    public void ComputeCacheKey_RejectsUnboundedEntryType()
    {
        Assert.Throws<BotBuildException>(() => BotBuilder.ComputeCacheKey(
            [new SourceFile("Bot.cs", "class Bot {}")],
            new string('A', 201)));
    }
}

