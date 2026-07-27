using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Deterministic contract probe: exercises dynamic typed actions when legal
/// and reports a life-local call counter so isolation can be verified.
/// </summary>
public sealed class FrontlineProbeBot : IActorBot
{
    private ActorIdentity? _actorId;
    private int _calls;

    public void StartLife(ActorMatchStart start)
    {
        _actorId = start.ActorId;
        _calls = 0;
    }

    public ActorDecision Tick(ActorContext context)
    {
        _calls++;
        context.Debug.Write($"actor={_actorId};calls={_calls}");

        ObservedActionAvailability? fabricate =
            context.Action(ActorActionIds.Fabricate);
        if (fabricate is
            {
                Available: true,
                AllowedUnitTargets: { Length: > 0 } targets,
            })
        {
            return Actions.Fabricate(targets[0]);
        }

        ObservedActionAvailability? transform =
            context.Action(ActorActionIds.Transform);
        if (transform is
            {
                Available: true,
                AllowedFormTargets: { Length: > 0 } forms,
            })
        {
            return Actions.Transform(forms[0]);
        }

        ObservedActionAvailability? directional =
            context.Action(ActorActionIds.ShootDirection);
        if (directional is
            {
                Available: true,
                AllowedProjectileHeadings: { Length: > 0 } headings,
            })
        {
            return Actions.ShootDirection(headings[0]);
        }

        ObservedActionAvailability? shoot =
            context.Action(ActorActionIds.Shoot);
        return shoot is { Available: true }
            ? Actions.Shoot()
            : Actions.Wait();
    }
}
