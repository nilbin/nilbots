namespace BotArena.Engine;

/// <summary>
/// Resolved actor, wall, and projectile collision semantics. Allied projectile
/// contact is a closed policy so friendly-fire and blocking flags cannot
/// contradict one another.
/// </summary>
public sealed record ActorCollisionDefinition
{
    public ActorCollisionDefinition(
        bool actorsBlockWalls,
        bool actorsBlockActors,
        bool sameDestinationMovesBlockAll,
        bool swapMovesBlocked,
        bool followingVacatedActorAllowed,
        bool projectilesBlockMovement,
        bool movingOntoProjectileCausesHit,
        bool wallsConsumeProjectiles,
        bool projectilesIgnoreFiringLife,
        bool projectilesStopOnFirstEnemyActor,
        bool projectilesCollideWithProjectiles,
        AlliedProjectileContactKind alliedProjectileContact)
    {
        if (!Enum.IsDefined(alliedProjectileContact))
        {
            throw new ArgumentOutOfRangeException(
                nameof(alliedProjectileContact));
        }
        if (!actorsBlockWalls
            || !actorsBlockActors
            || !sameDestinationMovesBlockAll
            || !swapMovesBlocked
            || followingVacatedActorAllowed
            || !projectilesBlockMovement
            || !movingOntoProjectileCausesHit
            || !wallsConsumeProjectiles
            || !projectilesIgnoreFiringLife
            || !projectilesStopOnFirstEnemyActor
            || projectilesCollideWithProjectiles)
        {
            throw new ArgumentException(
                "Generation 3 supports only the disclosed joint-grid collision " +
                "profile. Add a tagged executable profile before admitting " +
                "different collision semantics.");
        }

        ActorsBlockWalls = actorsBlockWalls;
        ActorsBlockActors = actorsBlockActors;
        SameDestinationMovesBlockAll = sameDestinationMovesBlockAll;
        SwapMovesBlocked = swapMovesBlocked;
        FollowingVacatedActorAllowed = followingVacatedActorAllowed;
        ProjectilesBlockMovement = projectilesBlockMovement;
        MovingOntoProjectileCausesHit = movingOntoProjectileCausesHit;
        WallsConsumeProjectiles = wallsConsumeProjectiles;
        ProjectilesIgnoreFiringLife = projectilesIgnoreFiringLife;
        ProjectilesStopOnFirstEnemyActor =
            projectilesStopOnFirstEnemyActor;
        ProjectilesCollideWithProjectiles =
            projectilesCollideWithProjectiles;
        AlliedProjectileContact = alliedProjectileContact;
    }

    public bool ActorsBlockWalls { get; }
    public bool ActorsBlockActors { get; }
    public bool SameDestinationMovesBlockAll { get; }
    public bool SwapMovesBlocked { get; }
    public bool FollowingVacatedActorAllowed { get; }
    public bool ProjectilesBlockMovement { get; }
    public bool MovingOntoProjectileCausesHit { get; }
    public bool WallsConsumeProjectiles { get; }
    public bool ProjectilesIgnoreFiringLife { get; }
    public bool ProjectilesStopOnFirstEnemyActor { get; }
    public bool ProjectilesCollideWithProjectiles { get; }
    public AlliedProjectileContactKind AlliedProjectileContact { get; }
    public MovementResolutionKind MovementResolution =>
        MovementResolutionKind
            .ExclusiveGroundOccupancyBlockConnectedConflictingClaims;
    public ProjectileTraversalResolutionKind ProjectileTraversalResolution =>
        ProjectileTraversalResolutionKind
            .CanonicalProjectileIdOrderedTileSubstepsWallsThenActors;
    public ActorProjectileContactTimingKind ActorProjectileContactTiming =>
        ActorProjectileContactTimingKind
            .MovementDestinationContactsThenExistingTraversalThenLaunchPath;

    public enum AlliedProjectileContactKind
    {
        PassThrough = 0,
        BlockWithoutDamage = 1,
        DamageAndBlock = 2,
    }

    public enum MovementResolutionKind
    {
        /// <summary>
        /// Ground actors occupy one exclusive floor tile. Same-destination,
        /// swap, following, and connected movement claims are resolved as one
        /// joint component; every conflicting claim in that component blocks.
        /// </summary>
        ExclusiveGroundOccupancyBlockConnectedConflictingClaims = 0,
    }

    public enum ProjectileTraversalResolutionKind
    {
        /// <summary>
        /// Projectiles resolve in numeric projectile-ID order. Each advance
        /// visits every path tile in order, checking range and wall contact
        /// before actor contact, so multi-tile advances cannot tunnel.
        /// </summary>
        CanonicalProjectileIdOrderedTileSubstepsWallsThenActors = 0,
    }

    public enum ActorProjectileContactTimingKind
    {
        /// <summary>
        /// Movement destination contacts are collected first, existing
        /// projectile path substeps second, and newly launched path tiles
        /// last. All resulting damage enters the shared joint batch.
        /// </summary>
        MovementDestinationContactsThenExistingTraversalThenLaunchPath = 0,
    }
}
