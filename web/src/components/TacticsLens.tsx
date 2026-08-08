import clsx from 'clsx';
import { useMemo, useState } from 'react';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import {
  playAwarenessTimeline,
  playsAt,
  type PlayActivation,
  type PlayPhase,
} from '../presentation/playAwareness';
import { teamName } from '../replayParticipants';
import { unitAccent } from '../render/unitPresentation';
import { teamVisionAt, teamVisionSeesActor } from '../render/teamVision';
import ClassIcon from './ClassIcon';

export default function TacticsLens({
  replay,
  tick,
  selectedActivationKey,
  onSelectActivation,
  onSelectUnit,
  onSeek,
  selectedUnitKey,
  showVisibility,
}: {
  replay: ReplayModel;
  tick: number;
  selectedActivationKey: string | null;
  onSelectActivation: (key: string | null) => void;
  onSelectUnit: (unitKey: ReplayStableUnitKey) => void;
  onSeek: (tick: number) => void;
  selectedUnitKey: ReplayStableUnitKey | null;
  showVisibility: boolean;
}) {
  const [open, setOpen] = useState(false);
  const timeline = useMemo(() => playAwarenessTimeline(replay), [replay]);
  const vision = teamVisionAt(
    replay,
    replay.ticks[tick],
    selectedUnitKey,
    showVisibility,
  );
  const active = playsAt(replay, tick).flatMap((play) => {
    if (vision === null || play.teamId === vision.teamId) return [play];
    const participants = play.participants.filter((participant) => {
      const actor = replay.ticks[tick]?.before.actors.find((candidate) =>
        candidate.actorKey === participant.actorKey,
      );
      return actor !== undefined && teamVisionSeesActor(vision, {
        actorKey: actor.actorKey,
        teamId: actor.identity.teamId,
        unitId: actor.identity.unitId,
      });
    });
    return participants.length === 0 ? [] : [{ ...play, participants }];
  });
  const selected = selectedActivationKey === null
    ? null
    : timeline.activations.find((entry) =>
      entry.key === selectedActivationKey && entry.startedTick <= tick,
    ) ?? null;
  const selectedFrames = selected === null
    ? []
    : timeline.frames.slice(0, tick + 1).flat().filter((entry) =>
      entry.activationKey === selected.key,
    );

  return (
    <div className="pointer-events-auto absolute bottom-2 left-2 z-[9] max-w-[min(440px,calc(100%-16px))] text-white">
      {!open ? (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="rounded-sm border border-white/15 bg-[#10161c]/88 px-2.5 py-1.5 text-[9px] font-bold uppercase tracking-[.17em] shadow-[0_4px_14px_rgba(0,0,0,.34)] backdrop-blur-[5px]"
          aria-expanded={false}
        >
          ◇ Tactics
        </button>
      ) : (
        <section
          aria-label="Tactics lens"
          className="min-w-[270px] rounded-sm border border-white/15 bg-[#10161c]/92 p-2 shadow-[0_5px_20px_rgba(0,0,0,.42)] backdrop-blur-[6px]"
        >
          <header className="flex items-center gap-2">
            <span className="text-[9px] font-bold uppercase tracking-[.17em] text-white/78">
              Tactics · tick {tick}
            </span>
            <button
              type="button"
              className="ml-auto text-[10px] text-white/55 hover:text-white"
              onClick={() => {
                setOpen(false);
                onSelectActivation(null);
              }}
              aria-label="Close tactics lens"
            >
              ×
            </button>
          </header>

          <div className="mt-2 grid gap-1">
            {active.length === 0 && (
              <p className="px-1 py-1 text-[9px] text-white/52">
                No published coordinated play at this tick.
              </p>
            )}
            {active.map((play) => {
              const canInspect = vision === null || play.teamId === vision.teamId;
              const unit = replay.units.find((candidate) =>
                candidate.teamId === play.teamId,
              );
              const accent = unit ? unitAccent(replay, unit.unitKey) : '#d9e6ee';
              return (
                <button
                  key={play.activationKey}
                  type="button"
                  onClick={() => {
                    if (!canInspect) return;
                    onSelectActivation(
                      selectedActivationKey === play.activationKey
                        ? null
                        : play.activationKey,
                    );
                  }}
                  disabled={!canInspect}
                  title={canInspect ? undefined : 'Opponent coordination is only shown while observed.'}
                  aria-pressed={selectedActivationKey === play.activationKey}
                  className={clsx(
                    'flex min-w-0 items-center gap-2 rounded-[2px] border px-2 py-1.5 text-left disabled:cursor-default',
                    selectedActivationKey === play.activationKey
                      ? 'border-white/28 bg-white/9'
                      : 'border-white/8 bg-black/16 hover:border-white/18',
                  )}
                >
                  <span
                    aria-hidden
                    className="flex size-5 shrink-0 items-center justify-center rotate-45 border"
                    style={{ borderColor: accent, boxShadow: `0 0 8px ${accent}55` }}
                  >
                    <i className="block size-1.5 bg-current" style={{ color: accent }} />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-[10px] font-semibold">
                      {play.name}
                      {play.ordinal > 1 ? ` #${play.ordinal}` : ''}
                    </span>
                    <span className="block truncate text-[8px] uppercase tracking-[.12em] text-white/52">
                      {teamName(replay, play.teamId)} · {play.phase}
                    </span>
                  </span>
                  <Composition replay={replay} unitKeys={play.participants.map((entry) => entry.unitKey)} />
                </button>
              );
            })}
          </div>

          {selected && (
            <PlayCard
              activation={selected}
              observedFrames={selectedFrames}
              tick={tick}
              onSelectUnit={onSelectUnit}
              onSeek={onSeek}
            />
          )}
        </section>
      )}
    </div>
  );
}

function PlayCard({
  activation,
  observedFrames,
  tick,
  onSelectUnit,
  onSeek,
}: {
  activation: PlayActivation;
  observedFrames: readonly ReturnType<typeof playsAt>[number][];
  tick: number;
  onSelectUnit: (unitKey: ReplayStableUnitKey) => void;
  onSeek: (tick: number) => void;
}) {
  const transitions = activation.transitions.filter((entry) => entry.tick <= tick);
  const contacts = activation.contacts.filter((entry) => entry.tick <= tick);
  const participantUnitKeys = [...new Set(
    observedFrames.flatMap((frame) => frame.participants.map((entry) => entry.unitKey)),
  )];
  const taskLabels = [...new Set(
    observedFrames.flatMap((frame) => frame.participants.map((entry) => entry.task)),
  )].sort();
  const firstUnit = participantUnitKeys[0] ?? null;
  const latestPhase = transitions.at(-1)?.phase ?? 'preparing';
  return (
    <div className="mt-2 border-t border-white/10 pt-2">
      <p className="text-[10px] font-semibold tracking-[.04em]">
        {activation.name.toUpperCase()} · {phaseLabel(latestPhase)}
      </p>
      <p className="mt-0.5 text-[8px] uppercase tracking-[.13em] text-white/42">
        entrant execution trace · activation {activation.ordinal}
      </p>
      <dl className="mt-2 grid grid-cols-[58px_1fr] gap-x-2 gap-y-1 text-[9px] leading-[1.35]">
        <dt className="uppercase tracking-[.1em] text-white/42">Prepared</dt>
        <dd>
          <Seek tick={activation.startedTick} onSeek={onSeek} /> · claimed{' '}
          {participantUnitKeys.length} bod{participantUnitKeys.length === 1 ? 'y' : 'ies'}
        </dd>
        {activation.committedTick !== null && activation.committedTick <= tick && (
          <>
            <dt className="uppercase tracking-[.1em] text-white/42">Committed</dt>
            <dd><Seek tick={activation.committedTick} onSeek={onSeek} /></dd>
          </>
        )}
        {contacts[0] && (
          <>
            <dt className="uppercase tracking-[.1em] text-white/42">Contact</dt>
            <dd><Seek tick={contacts[0].tick} onSeek={onSeek} /> · {contacts[0].summary}</dd>
          </>
        )}
        {activation.recoveryTick !== null && activation.recoveryTick <= tick && (
          <>
            <dt className="uppercase tracking-[.1em] text-white/42">Recovery</dt>
            <dd><Seek tick={activation.recoveryTick} onSeek={onSeek} /></dd>
          </>
        )}
        {activation.releaseTick !== null && activation.releaseTick <= tick && (
          <>
            <dt className="uppercase tracking-[.1em] text-white/42">Baseline</dt>
            <dd><Seek tick={activation.releaseTick} onSeek={onSeek} /> · survivors released</dd>
          </>
        )}
        <dt className="uppercase tracking-[.1em] text-white/42">Tasks</dt>
        <dd>{taskLabels.join(' · ') || 'no published task'}</dd>
      </dl>
      {!activation.named && (
        <p className="mt-2 text-[8px] leading-[1.35] text-white/45">
          The entrant published coordination and tasks, but no safe play name.
        </p>
      )}
      {firstUnit && (
        <button
          type="button"
          className="mt-2 rounded-[2px] border border-white/12 px-2 py-1 text-[8px] uppercase tracking-[.11em] text-white/65 hover:text-white"
          onClick={() => onSelectUnit(firstUnit)}
        >
          Show team vision
        </button>
      )}
    </div>
  );
}

function Composition({ replay, unitKeys }: {
  replay: ReplayModel;
  unitKeys: readonly ReplayStableUnitKey[];
}) {
  const classes = unitKeys.map((unitKey) =>
    replay.units.find((unit) => unit.unitKey === unitKey)?.classId,
  ).filter((classId): classId is string => Boolean(classId));
  return (
    <span className="flex shrink-0 -space-x-0.5" aria-label={`${classes.length} claimed bodies`}>
      {classes.slice(0, 4).map((classId, index) => (
        <ClassIcon
          key={`${classId}:${index}`}
          classId={classId}
          size={15}
          framed={false}
          decorative
        />
      ))}
    </span>
  );
}

function Seek({ tick, onSeek }: { tick: number; onSeek: (tick: number) => void }) {
  return (
    <button
      type="button"
      className="font-mono text-white/82 underline decoration-white/25 underline-offset-2 hover:text-white"
      onClick={() => onSeek(tick)}
    >
      T{tick}
    </button>
  );
}

function phaseLabel(phase: PlayPhase | 'released'): string {
  return phase === 'released' ? 'released' : phase;
}
