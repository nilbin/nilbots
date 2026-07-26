import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReplayDocument } from '../types';
import { usePlayback, useLiveFollower, type LiveFollow } from '../playback';
import { createPresenter } from '../replayPresentation';
import ArenaCanvas from './ArenaCanvas';

/**
 * The viewer reduced to its canvas, for an embedding host that draws its own chrome.
 *
 * The mobile app renders the transport, the control bar and the bot cards natively —
 * they are lists and buttons, and native ones scroll, scrub and feel right in a way a
 * WebView's cannot. What it cannot do natively is the arena itself: that is ~950 lines
 * of Canvas2D over megabytes of atlases, still moving, and worth exactly one
 * implementation.
 *
 * The playback clock stays on this side. The host asks for play/pause/seek and receives
 * a state message per tick; it does not drive `time` frame by frame, which would be a
 * bridge crossing per animation frame for something rAF already does locally.
 */
export default function HostedViewer({
  replay,
  live,
}: {
  replay: ReplayDocument;
  live?: LiveFollow;
}) {
  const playback = usePlayback(replay);
  const liveTime = useLiveFollower(replay, live);
  const presenter = useMemo(() => createPresenter(replay), [replay]);
  const [selectedSlot, setSelectedSlot] = useState<number | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);

  // A live broadcast is not played, it is followed: the server's presentation clock says
  // which tick every viewer is seeing, and the local clock only smooths between polls.
  // Seeking would desynchronise this viewer from everyone else, which is the one thing
  // broadcasting exists to prevent — so the host is told there is no transport.
  const following = live !== undefined;
  const time = following ? liveTime : playback.time;
  // Clamped to 0 when nothing has been released yet, so a countdown-phase replay
  // reports tick 0 rather than -1.
  const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));

  const post = useRef((message: Record<string, unknown>) => {
    window.ReactNativeWebView?.postMessage(JSON.stringify(message));
  }).current;

  // Commands in. Rebound whenever playback identity changes so the host never holds a
  // closure over a stale clock.
  useEffect(() => {
    window.__BOTARENA_CONTROL__ = {
      play: playback.play,
      pause: playback.pause,
      toggle: playback.toggle,
      restart: playback.restart,
      step: playback.step,
      seek: playback.seek,
      setSpeed: playback.setSpeed,
      selectSlot: (slot) => setSelectedSlot(slot),
      setVisibility: (visible) => setShowVisibility(visible),
    };
    return () => {
      delete window.__BOTARENA_CONTROL__;
    };
  }, [playback]);

  // The header never changes for a replay, so it is sent once rather than per tick.
  useEffect(() => {
    post({
      type: 'replay',
      header: {
        mapId: replay.header.mapId,
        seed: String(replay.header.seed),
        rulesVersion: replay.header.gameRulesVersion,
        replayHash: replay.replayHash ?? null,
        tickCount: presenter.tickCount,
        maxHealth: presenter.maxHealth,
        partial: replay.partial ?? false,
        participants: replay.header.participants.map((participant) => ({
          slot: participant.slot,
          name: participant.name,
          accent: participant.accent,
          lookId: participant.lookId,
        })),
      },
      result: replay.result
        ? {
            winnerSlot: replay.result.winnerSlot,
            reason: replay.result.reason,
            endTick: replay.result.endTick,
          }
        : null,
    });
  }, [replay, presenter, post]);

  // State out, once per tick rather than per frame: the host's readouts change on tick
  // boundaries, and at 5 ticks/second even 8x playback is a modest message rate.
  const lastSent = useRef<number | null>(null);
  useEffect(() => {
    if (lastSent.current === tick) return;
    lastSent.current = tick;
    post({ type: 'tick', ...presenter.at(tick) });
  }, [tick, presenter, post]);

  // Transport state changes on its own schedule, so it rides separately.
  useEffect(() => {
    post({
      type: 'transport',
      playing: following || playback.playing,
      speed: playback.speed,
      tick,
      tickCount: playback.tickCount,
      atEnd: !following && playback.atEnd,
      // The host hides its transport on this: a follower must not be able to seek away
      // from the moment every other viewer is on.
      following,
    });
  }, [following, playback.playing, playback.speed, playback.atEnd, tick, playback.tickCount, post]);

  return (
    <div className="relative h-screen w-screen bg-arena-bg">
      <ArenaCanvas
        replay={replay}
        time={time}
        selectedSlot={selectedSlot}
        showVisibility={showVisibility}
        onSelectSlot={(slot) => {
          setSelectedSlot(slot);
          // Tapping a bot on the canvas has to move the host's selection too, or the
          // native cards would disagree with what the arena is highlighting.
          post({ type: 'selected', slot });
        }}
      />
    </div>
  );
}
