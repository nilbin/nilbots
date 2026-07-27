import { Suspense, lazy, useEffect, useMemo, useRef, useState } from 'react';
import clsx from 'clsx';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import {
  participantForUnit,
  teamName,
} from '../replayParticipants';
import { ArenaAudioSession } from '../audio/ArenaAudioSession';
import { usePlayback, useLiveFollower, type LiveFollow } from '../playback';
import { useReplayAudio } from '../audio/useReplayAudio';
import { useAssetReadiness } from '../render/useAssetReadiness';
import { useImmersive } from './useImmersive';
import ArenaCanvas from './ArenaCanvas';

import AudioReviewControls from './AudioReviewControls';
import Controls from './Controls';
import BotPanel from './BotPanel';
import EventFeed from './EventFeed';
import Logo from './Logo';

/**
 * The 2.5D renderer, loaded only if asked for.
 *
 * `lazy` rather than a plain import so three.js lands in its own chunk: the Canvas2D
 * viewer is the default and must not pay for a renderer it does not use, and the CLI's
 * single-file artifact stubs this module out entirely (see vite.cli.config.ts).
 */
const ArenaCanvas3D = lazy(() => import('../render3d/ArenaCanvas3D'));
const AdaptiveSoundtrack = lazy(
  () => import('../soundtrack/AdaptiveSoundtrack'),
);
const DIMENSIONAL_RENDERER_AVAILABLE =
  typeof __BOTARENA_DIMENSIONAL_RENDERER__ !== 'boolean' ||
  __BOTARENA_DIMENSIONAL_RENDERER__;
const EXTERNAL_SOUNDTRACK_AVAILABLE =
  typeof __BOTARENA_EXTERNAL_SOUNDTRACK__ !== 'boolean' ||
  __BOTARENA_EXTERNAL_SOUNDTRACK__;

export default function Viewer({
  replay,
  live,
  soundtrackPresentationId,
}: {
  replay: ReplayModel;
  live?: LiveFollow;
  /** Stable match identity across partial-live and complete replay documents. */
  soundtrackPresentationId?: string;
}) {
  const assets = useAssetReadiness();
  const immersive = useImmersive();
  const shell = useRef<HTMLDivElement>(null);
  const isLive = live !== undefined;
  const audioSession = useMemo(() => new ArenaAudioSession(), []);
  // Immersive chrome fades out so nothing but the arena remains; any touch brings it back.
  const [chromeVisible, setChromeVisible] = useState(true);
  const playback = usePlayback(replay, assets.ready, !isLive);
  const liveTime = useLiveFollower(replay, live);
  const [selectedUnitKey, setSelectedUnitKey] =
    useState<ReplayStableUnitKey | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);
  // Opt-in, and sticky for the session so a reviewer comparing the two is not retyping a
  // query string. The Canvas2D renderer stays the default until this one has been judged
  // on a real screen — it is an alternative, not a replacement.
  const [dimensional, setDimensional] = useState(
    () =>
      DIMENSIONAL_RENDERER_AVAILABLE &&
      new URLSearchParams(window.location.search).get('renderer') ===
        '3d',
  );

  const audioReviewEnabled =
    new URLSearchParams(window.location.search).get('audio') !== 'off';
  const time = isLive ? liveTime : playback.time;
  const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
  const audio = useReplayAudio({
    replay,
    time,
    playing: isLive || playback.playing,
    speed: isLive ? 1 : playback.speed,
    atEnd: !isLive && playback.atEnd,
    following: isLive,
    reviewEnabled: audioReviewEnabled,
    session: audioSession,
  });

  useEffect(() => audioSession.retainOwner(), [audioSession]);

  useEffect(() => {
    if (isLive) return; // No seeking during a live broadcast — viewers stay synchronized.
    const onKey = (event: KeyboardEvent) => {
      if (event.target instanceof HTMLInputElement) return;
      switch (event.key) {
        case ' ':
          event.preventDefault();
          playback.toggle();
          break;
        case 'ArrowLeft':
          playback.step(-1);
          break;
        case 'ArrowRight':
          playback.step(1);
          break;
        case 'Home':
          playback.restart();
          break;
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [playback, isLive]);

  // Hold the chrome only while it is wanted. Paused playback keeps it up: someone who
  // stopped to look is not asking for the controls to vanish.
  useEffect(() => {
    if (!immersive.active || !chromeVisible || !playback.playing) return;
    const timer = window.setTimeout(() => setChromeVisible(false), 2_800);
    return () => window.clearTimeout(timer);
  }, [immersive.active, chromeVisible, playback.playing]);

  const { result } = replay;
  const winnerTeam =
    result?.winnerTeamId === null || result?.winnerTeamId === undefined
      ? null
      : replay.teams.find(
          (team) => team.teamId === result.winnerTeamId,
        ) ?? null;
  const winnerUnit = winnerTeam
    ? replay.units.find((unit) => unit.teamId === winnerTeam.teamId) ??
      null
    : null;
  const winnerParticipant = winnerUnit
    ? participantForUnit(replay, winnerUnit.unitKey)
    : null;

  return (
    <div
      ref={shell}
      onPointerDown={
        immersive.active
          ? () => {
              setChromeVisible(true);
              immersive.promote(shell.current);
            }
          : undefined
      }
      className={clsx(
        'relative mx-auto flex flex-col',
        immersive.active
          // The arena takes the whole viewport and the chrome floats over it. Merely
          // trimming padding gained nothing on a phone, where the grid was already one
          // column — the controls still claimed their own row and the arena stayed the
          // same size. 100dvh, not vh: Safari's toolbars collapse on scroll and vh does
          // not follow them.
          ? 'fixed inset-0 z-50 h-[100dvh] w-screen max-w-none gap-0 bg-arena-bg'
          : 'h-full max-w-7xl gap-3 p-3 md:p-5',
      )}
    >
      <header
        className={clsx(
          'flex flex-wrap items-baseline gap-x-4 gap-y-1',
          immersive.active && 'hidden',
        )}
      >
        <h1 className="text-xl"><Logo size={24} /></h1>
        <span className="font-mono text-xs text-arena-dim">
          {replay.map.mapId} · seed {replay.seed} · rules{' '}
          {replay.versions.gameRulesVersion} ·{' '}
          {replay.teams
            .map((team) => teamName(replay, team.teamId))
            .join(' vs ')}
        </span>
        {isLive ? (
          <span className="ml-auto flex items-center gap-1.5 rounded bg-red-500/15 px-2 py-0.5 font-mono text-[11px] font-bold text-red-400">
            <span className="inline-block size-2 animate-pulse rounded-full bg-red-500" />
            LIVE
          </span>
        ) : (
          replay.replayHash && (
            <span
              className="ml-auto font-mono text-[11px] text-arena-dim"
              title={`replay ${replay.replayHash}`}
            >
              #{replay.replayHash.slice(0, 12)}
            </span>
          )
        )}
        {EXTERNAL_SOUNDTRACK_AVAILABLE && (
          <Suspense fallback={null}>
            <AdaptiveSoundtrack
              replay={replay}
              time={time}
              playing={isLive || playback.playing}
              playResolveTail={!isLive && playback.endedNaturally}
              transportRevision={isLive ? 0 : playback.transportRevision}
              session={audioSession}
              presentationId={soundtrackPresentationId}
              followingLive={isLive}
            />
          </Suspense>
        )}
        {/* Which renderer. The CLI artifact intentionally stubs the dynamic module to
            keep three.js out of every copied replay, so it must not offer a blank mode. */}
        {DIMENSIONAL_RENDERER_AVAILABLE && (
          <button
            type="button"
            onClick={() => setDimensional((on) => !on)}
            className="rounded-md border border-arena-edge px-2 py-1 font-mono text-[11px] text-arena-dim transition-colors hover:border-arena-accent hover:text-arena-accent"
            aria-pressed={dimensional}
            title="Switch between the flat and the 2.5D renderer"
          >
            {dimensional ? '2.5D' : '2D'}
          </button>
        )}
        {/* Pointer devices only. A phone says what it wants by being turned, and a button
            that duplicated that would either fight the orientation or strand someone in a
            mode their device disagrees with. */}
        {immersive.offersToggle && (
          <button
            type="button"
            onClick={() => immersive.toggle(shell.current)}
            className="ml-auto rounded-md border border-arena-edge px-2 py-1 font-mono text-[11px] text-arena-dim transition-colors hover:border-arena-accent hover:text-arena-accent"
            aria-pressed={immersive.active}
          >
            {immersive.active ? 'exit full screen' : 'full screen'}
          </button>
        )}
      </header>

      {audioReviewEnabled && !immersive.active && (
        <AudioReviewControls
          audio={audio}
          onRestart={isLive ? undefined : playback.restart}
        />
      )}

      <div
        className={clsx(
          'grid min-h-0 flex-1 gap-3',
          // Immersive is the arena and nothing else. Panels were tried in the landscape
          // letterbox — they cost the arena no size, since it is height-constrained — but
          // a third of the screen given to text is not "mainly the game". The black bars
          // are aspect ratio, not waste, and framing beats clutter.
          immersive.active ? 'grid-cols-1' : 'grid-cols-1 lg:grid-cols-[1fr_320px]',
        )}
      >
        <main
          className={clsx(
            'relative min-h-[320px] overflow-hidden bg-arena-bg',
            // Edge to edge while immersive: a border and corner radius are panel styling,
            // and they cost real pixels on a phone where the arena is already letterboxed.
            immersive.active
              ? ''
              // **The arena decides how tall the arena is.** Both cells sit in one
              // auto-height grid row, so the row is as tall as its tallest cell — and with
              // the panel free to grow, that was the panel: the board visibly inflated
              // under the playhead as the event feed filled, more than doubling over a long
              // match. An aspect ratio gives the row a height that comes from the game
              // rather than from how much has happened in it. It applies at every width,
              // not just where the two are columns: stacked, the arena had only its
              // minimum height to fall back on and came out shorter than it used to be.
              : 'rounded-lg border border-arena-edge aspect-[16/10]',
          )}
        >
          {dimensional ? (
            <Suspense fallback={null}>
              <ArenaCanvas3D
                replay={replay}
                time={time}
                selectedUnitKey={selectedUnitKey}
                showVisibility={showVisibility}
                onSelectUnit={setSelectedUnitKey}
                onUnavailable={() => setDimensional(false)}
              />
            </Suspense>
          ) : (
            <ArenaCanvas
              replay={replay}
              time={time}
              selectedUnitKey={selectedUnitKey}
              showVisibility={showVisibility}
              onSelectUnit={setSelectedUnitKey}
            />
          )}
          {!assets.ready && (
            <div className="absolute inset-0 flex items-center justify-center bg-arena-bg/80">
              <p className="font-mono text-xs tracking-widest text-arena-dim" role="status">
                LOADING ARENA — {assets.pending} texture{assets.pending === 1 ? '' : 's'}
              </p>
            </div>
          )}
          {!isLive && playback.atEnd && result && (
            <div className="absolute inset-0 flex items-center justify-center bg-arena-bg/70">
              <div className="rounded-xl border border-arena-edge bg-arena-panel px-8 py-6 text-center shadow-2xl">
                <p className="font-mono text-xs tracking-widest text-arena-dim">
                  MATCH COMPLETE — {result.reason.toUpperCase()} · TICK {result.endTick}
                </p>
                <p className="mt-2 text-2xl font-black tracking-wide">
                  {winnerTeam ? (
                    <>
                      <span
                        style={{
                          color: winnerParticipant?.accent ?? '#38bdf8',
                        }}
                      >
                        {teamName(replay, winnerTeam.teamId)}
                      </span>{' '}
                      WINS
                    </>
                  ) : (
                    'DRAW'
                  )}
                </p>
                {result.teams.some((team) => team.zoneTicks !== null) && (
                  <p className="mt-1 font-mono text-xs text-arena-dim">
                    zone{' '}
                    {[...result.teams]
                      .sort((left, right) => left.teamId - right.teamId)
                      .map(
                        (team) =>
                          `${teamName(replay, team.teamId)} ${team.zoneTicks ?? 0}`,
                      )
                      .join(' · ')}
                  </p>
                )}
                {result.objective.kind === 'legacy' &&
                  result.objective.controlPressure !== null && (
                  <p className="mt-1 font-mono text-xs text-arena-dim">
                    final control{' '}
                    {result.objective.controlPressure > 0 ? '+' : ''}
                    {result.objective.controlPressure}
                  </p>
                )}
                {result.objective.kind === 'frontline' && (
                  <p className="mt-1 font-mono text-xs text-arena-dim">
                    final position{' '}
                    {result.objective.activePositionIndex + 1}
                    {result.territorialScore === null
                      ? ''
                      : ` · territory ${result.territorialScore}`}
                  </p>
                )}
                <button
                  onClick={playback.restart}
                  className="mt-4 rounded-md border border-arena-accent px-4 py-1.5 font-mono text-sm text-arena-accent transition-colors hover:bg-arena-accent/15"
                >
                  ⟲ Watch again
                </button>
              </div>
            </div>
          )}
        </main>

        {/* Out of flow beside the arena, in flow beneath it.

            The inner wrapper is absolute at `lg`, which is where the two become columns:
            that makes this cell contribute nothing to the row's height, so a growing feed
            cannot stretch the arena. It is the same reason the arena canvas is absolute
            inside `main`. Stacked on a narrow screen there is no shared row to distort, and
            the panel is simply content below the game. */}
        <aside
          className={clsx('relative flex min-h-0 flex-col', immersive.active && 'hidden')}
        >
          <div className="flex min-h-0 flex-1 flex-col gap-3 lg:absolute lg:inset-0">
            <BotPanel
              replay={replay}
              tick={tick}
              selectedUnitKey={selectedUnitKey}
              showVisibility={showVisibility}
              onSelectUnit={setSelectedUnitKey}
              onToggleVisibility={() => setShowVisibility((value) => !value)}
            />
            <EventFeed replay={replay} tick={tick} />
          </div>
        </aside>
      </div>

      {/* Immersive: the transport floats over the arena rather than taking a row from it,
          which is the whole point — a row of controls under a phone-height canvas is the
          layout that made full screen pointless. Translucent and inset so it reads as
          over the arena rather than crowding it. */}
      <div
        className={clsx(
          immersive.active &&
            'pointer-events-none absolute inset-x-0 bottom-0 z-10 p-2 pb-[env(safe-area-inset-bottom)]',
        )}
      >
        <div
          className={clsx(
            immersive.active &&
              clsx(
                'pointer-events-auto transition-opacity duration-300',
                chromeVisible ? 'opacity-95' : 'opacity-0',
              ),
          )}
        >
          {isLive ? (
            <div className="flex items-center gap-3 rounded-lg border border-arena-edge bg-arena-panel p-4 font-mono text-xs text-arena-dim">
              <span className="inline-block size-2 animate-pulse rounded-full bg-red-500" />
              Broadcasting tick {String(tick).padStart(3, '0')} — every viewer sees this moment.
            </div>
          ) : (
            <Controls playback={playback} />
          )}
        </div>
      </div>

      {/* The only way out while the page chrome is hidden — on a pointer device. When the
          device's own orientation put us here, turning it back is the way out, and a
          button could only disagree with the phone it is drawn on. */}
      {immersive.active && !immersive.automatic && (
        <button
          type="button"
          onClick={immersive.exit}
          className={clsx(
            'absolute top-0 right-0 z-20 m-2 rounded-md border border-arena-edge bg-arena-panel/80 px-2.5 py-1.5 font-mono text-[11px] text-arena-dim backdrop-blur transition-opacity duration-300 hover:border-arena-accent hover:text-arena-accent',
            chromeVisible ? 'opacity-95' : 'opacity-0',
          )}
          style={{ marginTop: 'max(0.5rem, env(safe-area-inset-top))' }}
        >
          exit
        </button>
      )}
    </div>
  );
}
