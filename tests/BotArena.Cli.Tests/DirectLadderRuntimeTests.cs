using System.Text.Json;
using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class DirectLadderRuntimeTests
{
    [Fact]
    public void ArbitraryRoleAndRouteNamesRunWithoutAPlaybookSpecificBridge()
    {
        using JsonDocument source = JsonDocument.Parse("""
        {
          "schema": "arc-relay-tactical-playbook-v1",
          "playbookId": "synthetic-ladder-proof",
          "auditStatus": {
            "provisionalEvaluationOnly": true,
            "playerFacingProductSchema": false
          },
          "composition": [
            "kestrel", "kestrel", "kestrel", "kestrel",
            "kestrel", "kestrel", "kestrel", "kestrel"
          ],
          "layout": { "path": "unused.json", "sha256": "unused" },
          "memory": {
            "enemyUnavailableTicks": 10,
            "lastSeenEnemyTicks": 10,
            "securedCoreTicks": 10,
            "objectiveProgressTicks": 10
          },
          "roles": [{
            "roleId": "vanguard",
            "candidateClasses": ["kestrel"],
            "minimum": 1,
            "preferred": 8,
            "maximum": 8,
            "carrierPreference": "forbid",
            "deathPolicy": "hold-vacancy",
            "respawnPolicy": "rejoin",
            "overflowRoleId": "vanguard"
          }],
          "custodyPolicies": [],
          "doctrines": {
            "roles": {
              "vanguard": {
                "custody": "none",
                "modes": [
                  {
                    "assault": "breach-route",
                    "while": "ready",
                    "focus": { "maximumAttackers": 2 }
                  },
                  {
                    "patrol": "fallback-loop",
                    "focus": { "maximumAttackers": 1 }
                  }
                ]
              }
            },
            "cohorts": {},
            "focus": {}
          },
          "conditions": {
            "ready": {
              "fact": "self-level",
              "operator": "at-least",
              "value": 1
            }
          }
        }
        """);
        DirectLadderRuntime runtime = DirectLadderRuntime.Create(
            source.RootElement);
        TacticalTaskCandidate candidate = new(
            41,
            new ActorIdentity(0, 41, 3),
            "vanguard",
            "vanguard",
            "active",
            "kestrel",
            false,
            new Position(4, 7),
            Health: 3,
            Level: 2);

        Dictionary<int, TacticalPlaybookPackage.Order> selected =
            runtime.Resolve(
                12,
                [candidate],
                null!, // No cohort in this synthetic ladder needs geography.
                new HashSet<int>(),
                (_, _, _) => true);

        TacticalPlaybookPackage.Order order = Assert.Single(selected).Value;
        Assert.Equal("vanguard-breach-route", order.OrderId);
        Assert.Equal("breach-route", order.Movement.Target);
        Assert.Equal("rungs=41:vanguard-breach-route", runtime.TraceSummary);
        Assert.DoesNotContain("hunter", runtime.TraceSummary,
            StringComparison.OrdinalIgnoreCase);
    }
}
