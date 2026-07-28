namespace BotArena.Engine.Tests;

public class InitialDeploymentDefinitionTests
{
    [Fact]
    public void DeploymentBindsEveryInitialLifeToOneResolvedSpawn()
    {
        PublicMatchTopology topology = new()
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(20, 1)],
            UnitSlots = [new(0, 0, 10), new(1, 0, 20)],
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(1, 0, 0, "mobile"),
            ],
        };
        var deployment = new InitialDeploymentDefinition(
            [
                new("east", new Position(7, 2), Direction.West),
                new("west", new Position(1, 2), Direction.East),
            ],
            [
                new(1, 0, 0, "mobile", "east"),
                new(0, 0, 0, "mobile", "west"),
            ]);

        deployment.ValidateTopology(topology);

        Assert.Equal(["east", "west"],
            deployment.Spawns.Select(spawn => spawn.SpawnId).ToArray());
        Assert.Equal(
            [(0, 0, "west"), (1, 0, "east")],
            deployment.Lives
                .Select(life => (life.TeamId, life.UnitId, life.SpawnId))
                .ToArray());
    }

    [Fact]
    public void DeploymentRejectsAFormOrLifeThatDoesNotMatchTopology()
    {
        PublicMatchTopology topology = new()
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(20, 1)],
            UnitSlots = [new(0, 0, 10), new(1, 0, 20)],
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(1, 0, 0, "mobile"),
            ],
        };
        var deployment = new InitialDeploymentDefinition(
            [
                new("east", new Position(7, 2), Direction.West),
                new("west", new Position(1, 2), Direction.East),
            ],
            [
                new(0, 0, 0, "turret", "west"),
                new(1, 0, 0, "mobile", "east"),
            ]);

        Assert.Throws<ArgumentException>(() =>
            deployment.ValidateTopology(topology));
    }

    [Fact]
    public void DeploymentRejectsTwoInitialLivesInOneStableSlot()
    {
        Assert.Throws<ArgumentException>(() =>
            new InitialDeploymentDefinition(
                [
                    new("first", new Position(1, 2), Direction.East),
                    new("second", new Position(2, 2), Direction.East),
                ],
                [
                    new(0, 0, 0, "mobile", "first"),
                    new(0, 0, 1, "mobile", "second"),
                ]));
    }
}
