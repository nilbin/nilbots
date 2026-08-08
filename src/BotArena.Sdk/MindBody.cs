using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// One body this mind commands, and the surface it is commanded through.
///
/// <para><b>Read the state, then write a command onto the same object.</b> The
/// identity and gameplay fields below are exactly what a per-life bot saw about
/// itself, plus the handful of facts a mind is entitled to and a per-life bot
/// was not: <see cref="PreviousPosition"/>, <see cref="MovedLastTick"/>,
/// <see cref="LifeStartedTick"/>, <see cref="Origin"/> and
/// <see cref="RoleTag"/>. Commands are buffered on this handle and harvested
/// after <see cref="IGenericMindBot.Think"/> returns.</para>
///
/// <para><b>The default is Wait.</b> Every own live body is pre-filled with the
/// contract's wait action before your <c>Think</c> runs, so a body you never
/// touch costs one tick and nothing else. That is deliberate: under one mind
/// driving nine bodies, an exact-key rule would turn an ergonomics slip into a
/// lost match, and removing exactly that class of bug is why this profile
/// exists.</para>
///
/// <para><b>One command per body per tick.</b> A second
/// <see cref="Command(string, int, GenericActorActionArgument[])"/> or
/// <see cref="Hold(string?)"/> on the same body throws immediately, inside your
/// own code, where you can see it — rather than silently letting the last
/// writer win and leaving you to find it in a replay. Setting a role tag is not
/// a command and may be done independently, in either order.</para>
/// </summary>
public sealed class MindBody
{
    private readonly MindWaitAction _waitAction;
    private BufferedCommand? _command;
    private string? _roleTag;
    private bool _roleTagSet;

    internal MindBody(
        ActorIdentity actorId,
        int generation,
        string formId,
        Position position,
        Direction facing,
        int health,
        int cooldown,
        int? energy,
        GenericActorActionResolution? previousActionResolution,
        GenericActorContext.PendingSameLifeTransition?
            pendingSameLifeTransition,
        string? classId,
        ImmutableArray<GenericActorContext.ObservedRouteCooldown>
            routeCooldowns,
        int carriedScrap,
        Position? previousPosition,
        bool movedLastTick,
        int lifeStartedTick,
        GenericActorMatchStart.LifeOrigin origin,
        string? roleTag,
        ulong bodyRandomSeed,
        ImmutableArray<GenericActorActionLegality> actionLegalities,
        MindWaitAction waitAction)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentOutOfRangeException.ThrowIfNegative(cooldown);
        ArgumentOutOfRangeException.ThrowIfNegative(carriedScrap);
        ArgumentOutOfRangeException.ThrowIfNegative(lifeStartedTick);
        if (health <= 0)
            throw new ArgumentOutOfRangeException(nameof(health));

        ActorId = actorId;
        Generation = generation;
        FormId = GenericActorDynamicValueRules.SemanticId(
            formId,
            nameof(formId));
        Position = position;
        Facing = GenericActorDynamicValueRules.EnumValue(
            facing,
            nameof(facing));
        Health = health;
        Cooldown = cooldown;
        Energy = energy;
        PreviousActionResolution = previousActionResolution;
        PendingSameLifeTransition = pendingSameLifeTransition;
        ClassId = classId is null
            ? null
            : GenericActorDynamicValueRules.SemanticId(
                classId,
                nameof(classId));
        RouteCooldowns = routeCooldowns.IsDefault ? [] : routeCooldowns;
        CarriedScrap = carriedScrap;
        PreviousPosition = previousPosition;
        MovedLastTick = movedLastTick;
        LifeStartedTick = lifeStartedTick;
        Origin = origin;
        RoleTag = roleTag is null
            ? null
            : MindValueRules.RoleTag(roleTag, nameof(roleTag));
        BodyRandomSeed = bodyRandomSeed;
        ActionLegalities = GenericActorDynamicValueRules.Snapshot(
            actionLegalities,
            nameof(actionLegalities));
        _waitAction = waitAction;
    }

    /// <summary>Exact body-life identity: team, stable unit slot, and life.</summary>
    public ActorIdentity ActorId { get; }

    /// <summary>
    /// The STABLE team-local handle, which survives this body's death. Key your
    /// plans by this, not by <see cref="ActorIdentity.LifeId"/>: a courier run
    /// assigned to unit 3 can be handed to unit 5 when unit 3 dies, and the
    /// replay viewer keeps the same panel card across the respawn.
    /// </summary>
    public int UnitId => ActorId.UnitId;

    /// <summary>Replication/return generation for this life.</summary>
    public int Generation { get; }

    /// <summary>Current form catalog identifier.</summary>
    public string FormId { get; }

    /// <summary>
    /// This body's chassis, or <see langword="null"/> on a classless contract.
    /// Under a mixed composition, bodies in the same army carry DIFFERENT kits —
    /// prefer conditioning on <see cref="ActionLegalities"/> over the class
    /// name.
    /// </summary>
    public string? ClassId { get; }

    /// <summary>Current map tile coordinate.</summary>
    public Position Position { get; }

    /// <summary>Current absolute map direction.</summary>
    public Direction Facing { get; }

    /// <summary>Current positive health in health points.</summary>
    public int Health { get; }

    /// <summary>Ticks remaining on the attack cooldown.</summary>
    public int Cooldown { get; }

    /// <summary>
    /// Current attack energy, or <see langword="null"/> when the current form
    /// has no energy-bearing attack profile.
    /// </summary>
    public int? Energy { get; }

    /// <summary>
    /// Scrap this body is carrying — exactly what a death on this tile would put
    /// on the floor. Zero for the whole match on a contract with no declared
    /// economy.
    /// </summary>
    public int CarriedScrap { get; }

    /// <summary>
    /// Every same-life route of this body's SLOT currently held shut by a route
    /// cooldown, with the first tick each accepts a request again. The clock is
    /// slot-scoped, so it survives this body's death.
    /// </summary>
    public ImmutableArray<GenericActorContext.ObservedRouteCooldown>
        RouteCooldowns
    { get; }

    /// <summary>Current same-life transition windup, if any.</summary>
    public GenericActorContext.PendingSameLifeTransition?
        PendingSameLifeTransition
    { get; }

    /// <summary>
    /// What happened to this body's previous command, or
    /// <see langword="null"/> on a life's first tick. It carries the submitted,
    /// accepted and validated action plus the outcome, so a <c>Blocked</c> move
    /// is legible rather than inferred from an unchanged position.
    /// </summary>
    public GenericActorActionResolution? PreviousActionResolution { get; }

    /// <summary>
    /// Where this body stood at the end of the previous tick, or
    /// <see langword="null"/> on a life's first tick.
    /// </summary>
    public Position? PreviousPosition { get; }

    /// <summary>
    /// Whether this body changed tile last tick. Published because it is the
    /// fact the capture channel turns on and because reconstructing it by hand
    /// was the single most-requested platform addition — every reconstruction
    /// had to be updated on every early return or be wrong in exactly the way
    /// that is hardest to see. It is <see langword="false"/> on a life's first
    /// tick, when there is no previous tile to have left.
    /// </summary>
    public bool MovedLastTick { get; }

    /// <summary>The tick on which this life first existed.</summary>
    public int LifeStartedTick { get; }

    /// <summary>
    /// Why this body exists, and its lineage. Under the mind this is a per-BODY
    /// fact delivered on the tick the body first appears, rather than a
    /// start-time fact about the program: one mind sees initial deployments,
    /// automatic returns, fabrications and replications arrive over the whole
    /// match.
    /// </summary>
    public GenericActorMatchStart.LifeOrigin Origin { get; }

    /// <summary>
    /// The label this mind last attached to this body, or
    /// <see langword="null"/> if it has never set one. Tags are sticky across
    /// ticks, so this is what the viewer is currently showing. Setting a new one
    /// is <see cref="SetRole"/>.
    /// </summary>
    public string? RoleTag { get; }

    /// <summary>
    /// This body's own deterministic random seed — the exact value the
    /// per-life profile would have handed this life.
    ///
    /// <para>You almost certainly want <see cref="MindContext.Random"/>
    /// instead: it is one stream for the whole match, which is the point of the
    /// profile. This is here because a body's private stream is a per-BODY fact
    /// under the mind, and because it is what lets a per-life bot hosted on
    /// this profile reproduce its own behaviour EXACTLY rather than merely
    /// closely — the migration adapter seeds each sub-brain from it.</para>
    ///
    /// <para>Deriving anything from it that another mind could also derive is
    /// pointless: it is private to this body, and re-derivable by the replay
    /// validator, so a forged value in a document is refused.</para>
    /// </summary>
    public ulong BodyRandomSeed { get; }

    /// <summary>
    /// This body's own pre-tick legality mask. It is PER BODY, not per army:
    /// in a mixed composition one body may anchor and another may not, and the
    /// mask is the only truthful answer to which is which. Availability cannot
    /// predict simultaneous conflicts with other bodies' commands.
    /// </summary>
    public ImmutableArray<GenericActorActionLegality> ActionLegalities { get; }

    /// <summary>Finds this body's legality entry for one action ID.</summary>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <returns>
    /// The matching entry, or <see langword="null"/> when this contract has no
    /// such action at all.
    /// </returns>
    public GenericActorActionLegality? Action(string actionId) =>
        ActionLegalities.FirstOrDefault(action =>
            string.Equals(action.ActionId, actionId, StringComparison.Ordinal));

    /// <summary>Whether a command has already been written onto this body.</summary>
    public bool HasCommand => _command is not null;

    /// <summary>
    /// Commands this body to take one catalog action this tick, replacing the
    /// pre-filled wait.
    /// </summary>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <param name="actionCode">Compact action code paired with the identifier.</param>
    /// <param name="arguments">
    /// At most one typed value per parameter kind; pass none for a
    /// parameterless action.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// This body already carries a command. One body takes one action per tick,
    /// and a duplicate is a bug worth surfacing immediately rather than
    /// resolving silently.
    /// </exception>
    public void Command(
        string actionId,
        int actionCode,
        params GenericActorActionArgument[] arguments) =>
        Command(actionId, actionCode, arguments, debugMessage: null);

    /// <summary>
    /// Commands this body using an entry from its own
    /// <see cref="ActionLegalities"/>, which is the form that cannot get the
    /// identifier and the code out of step.
    /// </summary>
    /// <param name="action">One entry from this body's legality mask.</param>
    /// <param name="arguments">Typed arguments for that action.</param>
    public void Command(
        GenericActorActionLegality action,
        params GenericActorActionArgument[] arguments)
    {
        ArgumentNullException.ThrowIfNull(action);
        Command(
            action.ActionId,
            action.ActionCode,
            arguments,
            debugMessage: null);
    }

    /// <summary>
    /// Commands this body and attaches bounded diagnostic text that rides into
    /// the replay beside the command.
    /// </summary>
    /// <param name="actionId">Stable action catalog identifier.</param>
    /// <param name="actionCode">Compact action code paired with the identifier.</param>
    /// <param name="arguments">Typed arguments for that action.</param>
    /// <param name="debugMessage">
    /// Optional bounded text. It cannot affect simulation state and is never an
    /// action parameter.
    /// </param>
    public void Command(
        string actionId,
        int actionCode,
        IEnumerable<GenericActorActionArgument> arguments,
        string? debugMessage)
    {
        RequireUncommanded();
        _command = new BufferedCommand(
            GenericActorDynamicValueRules.SemanticId(
                actionId,
                nameof(actionId)),
            actionCode,
            GenericActorDynamicValueRules.Snapshot(
                arguments,
                nameof(arguments)),
            debugMessage);
    }

    /// <summary>
    /// Explicitly holds this body on the contract's wait action — the same
    /// action the host pre-fills, stated on purpose.
    ///
    /// <para>Holding is a real decision in this game, not the absence of one:
    /// a stationary body is what claims a contested point under the capture
    /// channel. Saying it explicitly, with a reason, is what makes the replay
    /// legible and distinguishes "this body is claiming" from "this mind forgot
    /// this body".</para>
    /// </summary>
    /// <param name="why">
    /// Optional bounded diagnostic text recorded beside the hold.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// This body already carries a command, or the contract declares no wait
    /// action.
    /// </exception>
    public void Hold(string? why = null)
    {
        RequireUncommanded();
        _command = new BufferedCommand(
            _waitAction.RequireActionId(),
            _waitAction.ActionCode,
            [],
            why);
    }

    /// <summary>
    /// Publishes this body's job as a free-vocabulary label.
    ///
    /// <para>The tag is entirely non-authoritative: the engine never reads it,
    /// it is never an action parameter, and it cannot change a single point of
    /// simulation state. What it does is make the strategy legible — to the
    /// replay viewer, to your own debugging, and to the OPPONENT, because tags
    /// are published on visible enemy bodies too. That last part is deliberate:
    /// a label the engine ignores and the enemy can read is a free deception
    /// channel, so calling your channeler a screen is a real move.</para>
    ///
    /// <para>Tags are sticky. A tag set on tick 40 keeps being published every
    /// tick until you change it, so a role assignment costs one call, not one
    /// call per tick. Pass <see cref="string.Empty"/> to clear it and
    /// <see langword="null"/> to leave it unchanged. Use the words your own
    /// doctrine uses — <c>channeler</c>, <c>screen</c>, <c>courier</c>,
    /// <c>bait</c> — because the vocabulary you chose IS the strategy made
    /// visible.</para>
    /// </summary>
    /// <param name="roleTag">
    /// Lowercase kebab-case label of at most 24 UTF-8 bytes, the empty string
    /// to clear, or <see langword="null"/> to leave the current tag alone.
    /// </param>
    public void SetRole(string? roleTag)
    {
        if (roleTag is null)
            return;
        _roleTag = MindValueRules.RoleTag(roleTag, nameof(roleTag));
        _roleTagSet = true;
    }

    /// <summary>
    /// Harvests what the mind wrote onto this body, or <see langword="null"/>
    /// when it wrote nothing at all and the host's pre-filled wait stands.
    /// </summary>
    internal MindCommand? HarvestCommand()
    {
        if (_command is null && !_roleTagSet)
            return null;

        // A tag with no action still needs a frame entry to ride on, and the
        // honest action for it is the wait the host already pre-filled:
        // stating it changes no outcome and publishes the label.
        BufferedCommand command = _command
            ?? new BufferedCommand(
                _waitAction.RequireActionId(),
                _waitAction.ActionCode,
                [],
                null);
        return new MindCommand(
            ActorId.UnitId,
            ActorId.LifeId,
            command.ActionId,
            command.ActionCode,
            command.Arguments,
            _roleTagSet ? _roleTag : null,
            command.DebugMessage);
    }

    private void RequireUncommanded()
    {
        if (_command is not null)
        {
            throw new InvalidOperationException(
                $"Body {ActorId} already carries a command this tick. One body "
                + "takes one action per tick; decide before you write.");
        }
    }

    private sealed record BufferedCommand(
        string ActionId,
        int ActionCode,
        ImmutableArray<GenericActorActionArgument> Arguments,
        string? DebugMessage);
}

/// <summary>
/// The contract's wait action, resolved once at match start and handed to every
/// body so <see cref="MindBody.Hold(string?)"/> never has to search the catalog
/// mid-tick.
/// </summary>
/// <param name="ActionId">
/// The wait action's identifier, or <see langword="null"/> on the pathological
/// contract that declares none.
/// </param>
/// <param name="ActionCode">Its paired compact code.</param>
public readonly record struct MindWaitAction(string? ActionId, int ActionCode)
{
    internal string RequireActionId() =>
        ActionId
        ?? throw new InvalidOperationException(
            "This contract declares no wait action, so a body cannot be held "
            + "explicitly. Command one of the actions in its legality mask.");
}
