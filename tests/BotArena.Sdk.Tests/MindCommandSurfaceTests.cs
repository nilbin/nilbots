using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

/// <summary>
/// The ergonomics contract, pinned. Writing commands onto bodies rather than
/// returning a map is the API's whole argument, so its edges — the default
/// wait, the immediate duplicate throw, the sticky tag — need to be facts
/// rather than intentions.
/// </summary>
public sealed class MindCommandSurfaceTests
{
    [Fact]
    public void ABodyTheMindNeverTouchesHarvestsNothingAndKeepsThePreFilledWait()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();

        Assert.Empty(mind.HarvestCommands());
    }

    [Fact]
    public void HoldIssuesTheContractsWaitActionWithItsReason()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();
        mind.Bodies[0].Hold("stationary claim");

        MindCommand command = Assert.Single(mind.HarvestCommands());

        Assert.Equal("wait", command.ActionId);
        Assert.Equal(0, command.ActionCode);
        Assert.Equal("stationary claim", command.DebugMessage);
        Assert.Equal(mind.Bodies[0].UnitId, command.UnitId);
        Assert.Equal(mind.Bodies[0].ActorId.LifeId, command.LifeId);
    }

    [Fact]
    public void ASecondCommandOnOneBodyThrowsWhereTheAuthorCanSeeIt()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();
        MindBody body = mind.Bodies[0];
        body.Command("move", 1);

        // Silently letting the last writer win would turn a real bug into a
        // replay mystery. One body takes one action per tick.
        Assert.Throws<InvalidOperationException>(() => body.Command("wait", 0));
        Assert.Throws<InvalidOperationException>(() => body.Hold());
        Assert.True(body.HasCommand);
    }

    [Fact]
    public void SettingARoleOnAnUncommandedBodyStillPublishesTheTag()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();
        mind.Bodies[0].SetRole("bait");

        MindCommand command = Assert.Single(mind.HarvestCommands());

        // The tag needs a frame entry to ride on, and the honest action for it
        // is the wait the host already pre-filled: stating it changes no
        // outcome and publishes the label.
        Assert.Equal("wait", command.ActionId);
        Assert.Equal("bait", command.RoleTag);
    }

    [Fact]
    public void ARoleTagRidesTheCommandTheMindActuallyWrote()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();
        MindBody body = mind.Bodies[0];
        body.SetRole("courier-out");
        body.Command(
            "move",
            1,
            new GenericActorActionArgument.DirectionArgument(Direction.North));

        MindCommand command = Assert.Single(mind.HarvestCommands());

        Assert.Equal("move", command.ActionId);
        Assert.Equal("courier-out", command.RoleTag);
        Assert.Single(command.Arguments);
    }

    [Fact]
    public void SetRoleWithNullLeavesTheCurrentTagAlone()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();
        MindBody body = mind.Bodies[0];
        body.Hold();
        body.SetRole(null);

        MindCommand command = Assert.Single(mind.HarvestCommands());

        // Null is "unchanged", not "clear": stickiness is what makes a role
        // assignment cost one call rather than one call per tick.
        Assert.Null(command.RoleTag);
        Assert.Equal("channeler", body.RoleTag);
    }

    [Fact]
    public void CommandingThroughTheLegalityMaskCannotDesyncIdAndCode()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();
        MindBody body = mind.Bodies[0];
        GenericActorActionLegality wait = body.Action("wait")!;
        body.Command(wait);

        MindCommand command = Assert.Single(mind.HarvestCommands());

        Assert.Equal(wait.ActionId, command.ActionId);
        Assert.Equal(wait.ActionCode, command.ActionCode);
    }

    [Fact]
    public void BodyLookupIsByStableUnitSoAPlanSurvivesARespawn()
    {
        MindContext mind = GenericMindDynamicTestFixture.Context();

        Assert.True(mind.TryBody(1, out MindBody found));
        Assert.Equal(1, found.UnitId);
        Assert.False(mind.TryBody(7, out _));
        Assert.Null(mind.Body(7));
        Assert.NotNull(mind.Slot(2));
    }

    [Fact]
    public void HoldingOnAContractWithNoWaitActionSaysSoPlainly()
    {
        MindBody body = new(
            new ActorIdentity(0, 0, 0),
            0,
            "mobile",
            new Position(1, 1),
            Direction.North,
            3,
            0,
            null,
            null,
            null,
            null,
            [],
            0,
            null,
            false,
            0,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                0,
                null,
                null,
                null),
            null,
            0,
            [],
            new MindWaitAction(null, 0));

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => body.Hold());
        Assert.Contains("no wait action", error.Message, StringComparison.Ordinal);
    }
}
