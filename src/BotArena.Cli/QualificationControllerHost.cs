using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Sdk;

namespace BotArena.Cli;

/// <summary>
/// Hosts one SDK-only qualification controller on whichever contract profile
/// the suite is running.
///
/// <para>The controllers themselves — passive wait, one-shot, repeated
/// pressure, entry sentinel — stay exactly what they were:
/// <see cref="IGenericActorBot"/> programs whose behaviour is the probe's
/// stated scenario. On the mind profile they cannot be handed to the session
/// directly, because a mind match has one runtime per participant, so they run
/// through the guest's own <c>WrappedPerLifeMind</c> adapter. That is the same
/// facade a submitted per-life artifact gets on the mind profile, which is the
/// point: the probe's opponent behaves identically on both suites, and any
/// difference a mind suite measures is a difference in the SUBJECT.</para>
///
/// <para>The recorded controller fingerprint carries the profile, because a
/// wrapped controller and a native one are not the same opponent identity even
/// when they are the same program, and qualification evidence must never have
/// to guess which it faced.</para>
/// </summary>
internal sealed class QualificationControllerHost : IDisposable
{
    private readonly IGenericActorRuntimeFactory? _actorFactory;
    private readonly IGenericMindRuntimeFactory? _mindFactory;

    private QualificationControllerHost(
        IGenericActorRuntimeFactory? actorFactory,
        IGenericMindRuntimeFactory? mindFactory)
    {
        _actorFactory = actorFactory;
        _mindFactory = mindFactory;
    }

    public static QualificationControllerHost Create(
        string controllerName,
        Func<IGenericActorBot> controller,
        bool mindProfile) =>
        mindProfile
            ? new QualificationControllerHost(
                null,
                new InProcessGenericMindRuntimeFactory(
                    () => Guest.GuestHost.WrapPerLife(
                        controllerName,
                        _ => controller())))
            : new QualificationControllerHost(
                new InProcessGenericActorRuntimeFactory(controller),
                null);

    public GenericActorParticipantConfiguration ToParticipant(
        int participantId,
        int teamId,
        string name,
        string fingerprint) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = name,
            RuntimeFactory = _actorFactory,
            MindRuntimeFactory = _mindFactory,
            RuntimeKind = "in-process-qualification-controller",
            ArtifactHash = fingerprint,
            Accent = "#f97316",
            LookId = "bastion",
            ProjectileLookId = "ember-lance",
        };

    /// <summary>
    /// The recorded controller identity, suffixed on the mind profile because
    /// the same program hosted a different way is a different opponent.
    /// </summary>
    public static string Fingerprint(string baseFingerprint, bool mindProfile) =>
        mindProfile ? $"{baseFingerprint}-mind" : baseFingerprint;

    public void Dispose()
    {
        _actorFactory?.Dispose();
        _mindFactory?.Dispose();
    }
}
