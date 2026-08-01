import { Suspense, lazy, useEffect, useMemo, useRef, useState } from 'react';
import clsx from 'clsx';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import {
  teamName,
  unitName,
  visualIndexForUnit,
} from '../replayParticipants';
import { unitAccent, unitLook } from '../render/unitPresentation';
import { ArenaAudioSession } from '../audio/ArenaAudioSession';
import { usePlayback, useLiveFollower, type LiveFollow } from '../playback';
import { useReplaySoundEffects } from '../audio/useReplaySoundEffects';
import { useAssetReadiness } from '../render/useAssetReadiness';
import { viewerGate } from '../render/viewerReadiness';
import { readSoundtrackEnabledPreference } from '../soundtrack/preferences';
import { useImmersive } from './useImmersive';
import { useScreenWakeLock } from './useScreenWakeLock';
import ArenaCanvas from './ArenaCanvas';
import PlayOverlay from './PlayOverlay';

import SoundEffectsControl from './SoundEffectsControl';
import CameraFitToggle from './CameraFitToggle';
import Controls from './Controls';
import BotPanel from './BotPanel';
import EventFeed from './EventFeed';
import Logo from './Logo';
import IdentityChip from './IdentityChip';
import { playerAccent } from '../presentation/playerAccent';
import { styleVariables } from '../presentation/styleVariables';
import LiveStatus, { LiveDot } from './LiveStatus';
import ArcRelayStory from './ArcRelayStory';

/**
 * The hosted viewer's 3D renderer — what the web viewer is.
 *
 * It stays `lazy` so Three.js remains in its own chunk and Canvas2D can load as
 * the no-WebGL floor. The CLI's single-file artifact stubs this module out
 * entirely (see vite.cli.config.ts), so a copied replay never carries it.
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
  const audioActivationInFlight = useRef(false);
  const [audioActivationGranted, setAudioActivationGranted] =
    useState(false);
  // Immersive chrome fades out so nothing but the arena remains; any touch brings it back.
  const [chromeVisible, setChromeVisible] = useState(true);
  // The WebGL renderer arrives as its own chunk and then builds a scene, and neither of
  // those is an asset the counter can see. Until the first frame has been drawn there is
  // nothing on screen, so readiness has to include it — otherwise the button lights while
  // the arena is still a black rectangle, which is the state this whole gate exists to
  // remove. Canvas2D needs no equivalent: it draws from the same atlases the counter
  // already holds, so it is ready the moment they are.
  const [sceneReady, setSceneReady] = useState(false);
  // Whether the viewer has been asked to play. The overlay is offered exactly once — the
  // transport owns pause and resume from there, and a scrim over a running match would be
  // worse than none.
  const [started, setStarted] = useState(false);
  const liveTime = useLiveFollower(replay, live);
  const [selectedUnitKey, setSelectedUnitKey] =
    useState<ReplayStableUnitKey | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);
  // The camera follows the fight by default, and any pan or zoom gesture drops that until
  // the toggle in the transport hands it back. Owned here rather than inside either
  // renderer because the two must never disagree about it — a device that loses its WebGL
  // context falls back to Canvas2D mid-replay, and the camera should not change its mind
  // about following at the same moment the arena changes how it is drawn.
  const [autoFit, setAutoFit] = useState(true);
  // The 3D renderer is the viewer now, and there is no way to ask for the flat one:
  // a dimension count was never a choice a player wanted to make.
  //
  // Canvas2D is not dead, it is just no longer a mode. It stays as the floor for the two
  // cases where this cannot draw at all — the CLI's single-file artifact, which stubs the
  // dynamic import out entirely (`vite.cli.config.ts` sets
  // `__BOTARENA_DIMENSIONAL_RENDERER__` false so three.js never enters a copied replay),
  // and a device that fails to give us a WebGL context, which is what `onUnavailable`
  // catches. Both fall back without asking.
  const [dimensional, setDimensional] = useState(DIMENSIONAL_RENDERER_AVAILABLE);

  const gate = viewerGate({
    assetsReady: assets.ready,
    dimensional,
    sceneReady,
    live: isLive,
    started,
  });
  // Never autostarted. A live broadcast is the exception and not a contradiction: its clock
  // is the server's, every viewer is on the same tick, and there is no transport to press.
  const playback = usePlayback(replay, gate.ready, !isLive, false);

  const soundEffectsAvailable =
    new URLSearchParams(window.location.search).get('audio') !== 'off';
  const time = isLive ? liveTime : playback.time;
  const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
  const soundEffects = useReplaySoundEffects({
    replay,
    time,
    playing: isLive || playback.playing,
    speed: isLive ? 1 : playback.speed,
    atEnd: !isLive && playback.atEnd,
    following: isLive,
    available: soundEffectsAvailable,
    activationGranted: audioActivationGranted,
    session: audioSession,
  });

  useEffect(() => audioSession.retainOwner(), [audioSession]);

  // The overlay is not the only way in — the transport has a play button, space toggles,
  // and a seek starts the clock too. Whichever route was taken, the invitation has been
  // accepted and must not be drawn over a running match again.
  useEffect(() => {
    if (playback.playing) setStarted(true);
  }, [playback.playing]);

  // A replay is minutes of watching with nothing to touch, so the phone dims and locks
  // mid-match. Held while the clock is running and given back the moment it stops — a
  // paused viewer left on a desk is not a reason to keep a screen awake. A live broadcast
  // counts as running: its clock is the server's, and there is no transport to press.
  useScreenWakeLock(isLive || playback.playing);

  useEffect(() => {
    if (audioActivationGranted) return;
    const soundtrackWantsAudio =
      EXTERNAL_SOUNDTRACK_AVAILABLE &&
      readSoundtrackEnabledPreference();
    if (!soundEffectsAvailable && !soundtrackWantsAudio) return;

    const requestActivation = () => {
      if (audioActivationInFlight.current) return;
      audioActivationInFlight.current = true;
      void audioSession.resume().then(
        () => {
          setAudioActivationGranted(true);
        },
        () => {
          // Keep the gate open so a later trusted interaction can retry.
          audioActivationInFlight.current = false;
        },
      );
    };
    const onClick = (event: MouseEvent) => {
      if (!event.isTrusted) return;
      requestActivation();
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (
        !event.isTrusted ||
        event.repeat ||
        event.isComposing ||
        event.altKey ||
        event.ctrlKey ||
        event.metaKey ||
        NON_ACTIVATING_AUDIO_KEYS.has(event.key)
      ) {
        return;
      }
      requestActivation();
    };

    window.addEventListener('click', onClick, true);
    window.addEventListener('keydown', onKeyDown, true);
    return () => {
      window.removeEventListener('click', onClick, true);
      window.removeEventListener('keydown', onKeyDown, true);
    };
  }, [
    audioActivationGranted,
    audioSession,
    soundEffectsAvailable,
  ]);

  useEffect(() => {
    if (isLive) return; // No seeking during a live broadcast — viewers stay synchronized.
    const onKey = (event: KeyboardEvent) => {
      const target = event.target;
      if (
        target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        (target instanceof HTMLElement && target.isContentEditable)
      ) {
        return;
      }
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
    const timer = window.setTimeout(() => {
      const focused = document.activeElement;
      if (
        focused instanceof HTMLElement &&
        focused !== document.body &&
        shell.current?.contains(focused)
      ) {
        return;
      }
      setChromeVisible(false);
    }, 2_800);
    return () => window.clearTimeout(timer);
  }, [immersive.active, chromeVisible, playback.playing]);

  useEffect(() => {
    if (!immersive.active) return;
    const revealForKeyboard = (event: KeyboardEvent) => {
      if (event.key === 'Tab') setChromeVisible(true);
    };
    window.addEventListener('keydown', revealForKeyboard, true);
    return () => window.removeEventListener('keydown', revealForKeyboard, true);
  }, [immersive.active]);

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
  const winnerAccent = winnerUnit
    ? unitAccent(replay, winnerUnit.unitKey)
    : null;
  const transport = isLive ? (
    // A live broadcast has no transport — every viewer is at the same tick — but the
    // camera is not transport, it is how this viewer is looking, so it stays offered.
    <div className="panel pad val flex min-w-0 items-center gap-2.5">
      <LiveDot className="size-2" />
      Broadcasting tick {String(tick).padStart(3, '0')} — every viewer sees
      this moment.
      <span className="ml-auto">
        <CameraFitToggle
          enabled={autoFit}
          onToggle={() => setAutoFit((value) => !value)}
        />
      </span>
    </div>
  ) : (
    <Controls
      playback={playback}
      replay={replay}
      selectedUnitKey={selectedUnitKey}
      autoFit={autoFit}
      onToggleAutoFit={() => setAutoFit((value) => !value)}
    />
  );

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
        'mx-auto flex min-w-0 flex-col',
        immersive.active
          // The arena takes the whole viewport and the chrome floats over it. Merely
          // trimming padding gained nothing on a phone, where the grid was already one
          // column — the controls still claimed their own row and the arena stayed the
          // same size. 100dvh, not vh: Safari's toolbars collapse on scroll and vh does
          // not follow them.
          ? 'fixed inset-0 z-50 h-[100dvh] max-w-none gap-0 bg-arena-bg'
          : 'relative h-full w-full max-w-7xl gap-3 p-3 md:p-5',
      )}
    >
      <header
        className={clsx(
          'relative min-w-0 flex flex-wrap items-center gap-x-3.5 gap-y-1',
          immersive.active && 'hidden',
        )}
      >
        <div aria-label="nilbots">
          <Logo size={22} />
        </div>
        {/* Who is fighting, not what the match is made of. The map, the seed, the rules
            version and the hash are provenance — they matter enormously, which is why
            they get a disclosure of their own rather than a byline nobody reads. */}
        <span className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
          {replay.teams.map((team, index) => {
            // A team's units, not a fixed pair: a duel shows one chip a side and a
            // Frontline team shows however many it fields.
            const unitKeys = replay.units
              .filter((unit) => unit.teamId === team.teamId)
              .map((unit) => unit.unitKey);
            return (
              <span key={team.teamKey} className="flex items-center gap-3">
                {index > 0 && <span className="lab">vs</span>}
                {unitKeys.slice(0, 3).map((unitKey) => (
                  <IdentityChip
                    key={unitKey}
                    // Resolved the way the arena resolves it: under a class arm
                    // every participant carries the same default look and accent,
                    // and the form catalog is the only thing that tells the two
                    // machines apart.
                    lookId={unitLook(replay, unitKey).id}
                    visualIndex={visualIndexForUnit(replay, unitKey)}
                    accent={unitAccent(replay, unitKey)}
                    name={unitName(replay, unitKey)}
                    nameClassName="text-[14px]"
                    size={22}
                  />
                ))}
                {unitKeys.length > 3 && (
                  <span className="val">+{unitKeys.length - 3}</span>
                )}
              </span>
            );
          })}
        </span>
        {isLive ? (
          <LiveStatus className="ml-auto" />
        ) : (
          <details className="group ml-auto">
            <summary className="btn cursor-pointer list-none text-arena-dim hover:text-arena-text">
              Verify
            </summary>
            {/* Determinism is the product's core claim, so the thing that lets anyone
                check it should be legible and copyable rather than a grey #de24f5aa in
                the corner. */}
            <dl className="panel pad absolute right-0 z-20 mt-2 grid grid-cols-[70px_1fr] items-baseline gap-x-3 gap-y-[7px]">
              <dt className="lab">Map</dt>
              <dd className="val text-arena-text">{replay.map.mapId}</dd>
              <dt className="lab">Seed</dt>
              <dd className="val text-arena-text">{String(replay.seed)}</dd>
              <dt className="lab">Ruleset</dt>
              <dd className="val text-arena-text">
                {replay.versions.gameRulesVersion}
              </dd>
              {replay.replayHash && (
                <>
                  <dt className="lab">Replay</dt>
                  <dd className="val break-all text-arena-text">
                    {replay.replayHash}
                  </dd>
                </>
              )}
            </dl>
          </details>
        )}
        {EXTERNAL_SOUNDTRACK_AVAILABLE && (
          <Suspense fallback={null}>
            <AdaptiveSoundtrack
              replay={replay}
              time={time}
              playing={isLive || playback.playing}
              playResolveTail={!isLive && playback.endedNaturally}
              playbackSpeed={isLive ? 1 : playback.speed}
              transportRevision={isLive ? 0 : playback.transportRevision}
              session={audioSession}
              activationGranted={audioActivationGranted}
              presentationId={soundtrackPresentationId}
              followingLive={isLive}
            />
          </Suspense>
        )}
        {soundEffectsAvailable && (
          <SoundEffectsControl effects={soundEffects} />
        )}
        {/* Pointer devices only. A phone says what it wants by being turned, and a button
            that duplicated that would either fight the orientation or strand someone in a
            mode their device disagrees with. */}
        {immersive.offersToggle && (
          <button
            type="button"
            onClick={() => immersive.toggle(shell.current)}
            className="btn ml-auto text-arena-dim hover:text-arena-text"
            aria-pressed={immersive.active}
          >
            {immersive.active ? 'exit full screen' : 'full screen'}
          </button>
        )}
      </header>

      <div
        className={clsx(
          'grid min-h-0 min-w-0 flex-1 gap-3',
          // Immersive is the arena and nothing else. Panels were tried in the landscape
          // letterbox — they cost the arena no size, since it is height-constrained — but
          // a third of the screen given to text is not "mainly the game". The black bars
          // are aspect ratio, not waste, and framing beats clutter.
          immersive.active
            ? 'grid-cols-1'
            : 'grid-cols-1 lg:grid-cols-[minmax(0,1fr)_300px]',
        )}
      >
        {/* The arena and transport are one column, as they are in the design guide.
            Keeping the transport in that column means a taller index can never land on
            top of it; the grid row simply grows, while the arena keeps its own ratio. */}
        <div
          className={clsx(
            'flex min-h-0 min-w-0 flex-col',
            immersive.active ? 'flex-1' : 'gap-3',
          )}
        >
          <section
            aria-label="Arena"
            className={clsx(
              'relative min-w-0 overflow-hidden bg-arena-bg sm:min-h-[320px]',
              // Edge to edge while immersive: a border and corner radius are panel styling,
              // and they cost real pixels on a phone where the arena is already letterboxed.
              immersive.active
                ? 'flex-1'
                // **The arena decides how tall the arena is.** The event index is bounded,
                // so its contents cannot inflate the board as playback advances.
                : 'rounded-[3px] border border-arena-edge aspect-[16/10]',
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
                onReady={() => setSceneReady(true)}
                autoFit={autoFit}
                onManualCamera={() => setAutoFit(false)}
              />
            </Suspense>
          ) : (
            <ArenaCanvas
              replay={replay}
              time={time}
              selectedUnitKey={selectedUnitKey}
              showVisibility={showVisibility}
              onSelectUnit={setSelectedUnitKey}
              autoFit={autoFit}
              onManualCamera={() => setAutoFit(false)}
            />
          )}
          <ArcRelayStory replay={replay} tick={tick} />
          {/* Where we are, over the game rather than under it: the eye is on the arena,
              and this is the one number a spectator is always reading. It is also the
              thing that must never disappear — when the immersive chrome fades, this
              goes to 40% instead of to nothing, because a viewer who cannot see the tick
              cannot tell how far in they are. */}
          <p
            className={clsx(
              'val absolute top-2 left-2 rounded-full border border-arena-edge bg-arena-bg/75 px-[9px] py-[2px] backdrop-blur-[3px] transition-opacity duration-300',
              immersive.active && !chromeVisible && 'opacity-40',
            )}
          >
            <span className="text-arena-text">
              {String(tick).padStart(3, '0')}
            </span>{' '}
            / {String(Math.max(0, replay.ticks.length - 1)).padStart(3, '0')}
          </p>
          {/* A live broadcast has no play button — the clock belongs to the server and
              every viewer is on the same tick — so it keeps a plain indicator. */}
          {isLive && gate.overlay === 'loading' && (
            <div className="absolute inset-0 flex items-center justify-center bg-arena-bg/80">
              <p className="lab" role="status">
                Loading arena
                {assets.pending > 0 && ` — ${assets.pending} asset${assets.pending === 1 ? '' : 's'}`}
              </p>
            </div>
          )}
          {!isLive && gate.overlay !== 'hidden' && (
            <PlayOverlay
              ready={gate.playable}
              pending={assets.pending}
              onPlay={() => {
                setStarted(true);
                playback.play();
              }}
            />
          )}
          {!isLive && playback.atEnd && result && (
            <div className="absolute inset-0 flex items-center justify-center bg-arena-bg/70">
              <div className="panel px-8 py-6 text-center">
                <p className="lab">
                  Match complete — {result.reason} · tick {result.endTick}
                </p>
                <p className="type-display mt-2 text-[21px]">
                  {winnerTeam ? (
                    <>
                      <span
                        className={
                          winnerAccent ? 'player-accent-text' : undefined
                        }
                        style={
                          winnerAccent
                            ? styleVariables({
                                '--player-accent': playerAccent(
                                  winnerAccent,
                                  'panel',
                                ),
                              })
                            : undefined
                        }
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
                  <p className="val mt-1">
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
                  <p className="val mt-1">
                    final control{' '}
                    {result.objective.controlPressure > 0 ? '+' : ''}
                    {result.objective.controlPressure}
                  </p>
                )}
                {result.objective.kind === 'frontline' && (
                  <p className="val mt-1">
                    final position{' '}
                    {result.objective.activePositionIndex + 1}
                    {result.mode?.kind === 'frontline'
                      ? ` · ${result.mode.scores
                          .map(
                            (score) =>
                              `${teamName(replay, score.teamId)} ${score.territorialProgress}`,
                          )
                          .join(' · ')}`
                      : result.territorialScore === null
                        ? ''
                        : ` · territory ${result.territorialScore}`}
                  </p>
                )}
                <button
                  type="button"
                  onClick={playback.restart}
                  className="btn mt-4"
                >
                  ⟲ Watch again
                </button>
              </div>
            </div>
          )}
          </section>

          {!immersive.active && transport}
        </div>

        {/* In flow beside the arena column, and beneath it on a narrow screen. The event
            list has its own cap and scroll, so the sidebar contributes a stable height
            without ever escaping the grid or covering the transport. */}
        <aside
          className={clsx(
            'relative flex min-h-0 min-w-0 flex-col',
            immersive.active && 'hidden',
          )}
        >
          <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-2.5">
            <BotPanel
              replay={replay}
              tick={tick}
              selectedUnitKey={selectedUnitKey}
              showVisibility={showVisibility}
              onSelectUnit={setSelectedUnitKey}
              onToggleVisibility={() => setShowVisibility((value) => !value)}
            />
            <EventFeed
              replay={replay}
              tick={tick}
              selectedUnitKey={selectedUnitKey}
              onSeek={isLive ? undefined : playback.seek}
            />
          </div>
        </aside>
      </div>

      {/* Immersive: the transport floats over the arena rather than taking a row from it,
          which is the whole point — a row of controls under a phone-height canvas is the
          layout that made full screen pointless. Translucent and inset so it reads as
          over the arena rather than crowding it.

          A scrim carries it rather than the panel alone: map themes are not all dark.
          gallery-01 is `frost-relay`, which is nearly white, and a hard-edged dark
          rectangle dropped on it reads as damage. A gradient darkens whatever is
          actually there — and it fades on the same element as the controls, so hidden
          chrome cannot leave a permanent band across the bottom of the arena.

          **The scrim never takes a tap; only the controls do.** It is a gradient with
          48px of transparent lead-in above the panel, drawn full-bleed and — because it
          carried `pointer-events-auto` — swallowing every touch in the bottom ~130px of
          the arena. On a desktop-shaped window that band is empty. On a phone held
          sideways the viewport is ~320-350px tall, and the band lands squarely on the
          end-of-match card's "Watch again" button and on the lower third of the play
          button: the first tap revealed the chrome, which turned the scrim's pointer
          events on, and every tap after that hit a gradient. "Watch again in full screen
          on my phone doesn't work" was literally that — a decoration eating the control
          underneath it. Decoration is `pointer-events-none`; the panel opts back in. */}
      {immersive.active && (
        <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10">
          <div
            className={clsx(
              'pointer-events-none bg-gradient-to-t from-arena-bg via-arena-bg/80 to-transparent p-2 pt-12 pb-[env(safe-area-inset-bottom)] transition-opacity duration-300',
              chromeVisible ? 'visible opacity-95' : 'invisible opacity-0',
            )}
          >
            <div className={chromeVisible ? 'pointer-events-auto' : undefined}>
              {transport}
            </div>
          </div>
        </div>
      )}

      {/* The only way out while the page chrome is hidden — on a pointer device. When the
          device's own orientation put us here, turning it back is the way out, and a
          button could only disagree with the phone it is drawn on. */}
      {immersive.active && !immersive.automatic && (
        <button
          type="button"
          onClick={immersive.exit}
          className={clsx(
            'btn safe-area-top absolute top-0 right-0 z-20 m-2 bg-arena-panel/80 text-arena-dim backdrop-blur transition-opacity duration-300 hover:text-arena-text',
            chromeVisible
              ? 'visible pointer-events-auto opacity-95'
              : 'invisible pointer-events-none opacity-0',
          )}
        >
          exit
        </button>
      )}
    </div>
  );
}

const NON_ACTIVATING_AUDIO_KEYS = new Set([
  'Alt',
  'CapsLock',
  'Control',
  'Escape',
  'Meta',
  'Shift',
  'Tab',
]);
