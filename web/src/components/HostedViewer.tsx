import { useEffect, useMemo, useRef, useState } from 'react';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import {
  usePlayback,
  useLiveFollower,
  type LiveFollow,
} from '../playback';
import { useAssetReadiness } from '../render/useAssetReadiness';
import { createPresenter } from '../replayPresentation';
import {
  replayBridgeMessage,
  selectedBridgeMessage,
  tickBridgeMessage,
  transportBridgeMessage,
  type HostedBridgeVersion,
} from '../hostedBridge';
import ArenaCanvas from './ArenaCanvas';

/**
 * Canvas-only viewer for an embedding host. Bridge 1 is the historical mobile
 * protocol and remains replay-v1/slot-only. Bridge 2 is explicit in the query
 * string and speaks stable units over the version-neutral ReplayModel.
 */
export default function HostedViewer({
  replay,
  live,
  bridgeVersion,
}: {
  replay: ReplayModel;
  live?: LiveFollow;
  bridgeVersion: HostedBridgeVersion;
}) {
  const assets = useAssetReadiness();
  const playback = usePlayback(replay, assets.ready);
  const liveTime = useLiveFollower(replay, live);
  const presenter = useMemo(() => createPresenter(replay), [replay]);
  const [selectedUnitKey, setSelectedUnitKey] =
    useState<ReplayStableUnitKey | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);

  const following = live !== undefined;
  const time = following ? liveTime : playback.time;
  const tick = Math.max(
    0,
    Math.min(Math.floor(time), replay.ticks.length - 1),
  );
  const post = useRef((message: Record<string, unknown>) => {
    window.ReactNativeWebView?.postMessage(JSON.stringify(message));
  }).current;

  useEffect(() => {
    const common = {
      play: playback.play,
      pause: playback.pause,
      toggle: playback.toggle,
      restart: playback.restart,
      step: playback.step,
      seek: playback.seek,
      setSpeed: playback.setSpeed,
      setVisibility: (visible: boolean) => setShowVisibility(visible),
    };
    window.__BOTARENA_CONTROL__ =
      bridgeVersion === 1
        ? {
            ...common,
            selectSlot: (slot: number | null) => {
              const unit =
                slot === null
                  ? null
                  : replay.units.find(
                      (candidate) => candidate.teamId === slot,
                    ) ?? null;
              setSelectedUnitKey(unit?.unitKey ?? null);
            },
          }
        : {
            ...common,
            selectUnit: (unitKey: ReplayStableUnitKey | null) => {
              setSelectedUnitKey(
                unitKey !== null &&
                  replay.units.some(
                    (candidate) => candidate.unitKey === unitKey,
                  )
                  ? unitKey
                  : null,
              );
            },
          };
    return () => {
      delete window.__BOTARENA_CONTROL__;
    };
  }, [bridgeVersion, playback, replay.units]);

  const lastSent = useRef<number | null>(null);
  useEffect(() => {
    lastSent.current = null;
    setSelectedUnitKey(null);
    post(
      replayBridgeMessage(
        bridgeVersion,
        replay,
        presenter.tickCount,
        presenter.maxHealth,
      ),
    );
  }, [bridgeVersion, replay, presenter, post]);

  useEffect(() => {
    if (lastSent.current === tick) return;
    lastSent.current = tick;
    post(tickBridgeMessage(bridgeVersion, presenter.at(tick)));
  }, [bridgeVersion, tick, presenter, post]);

  useEffect(() => {
    post(
      transportBridgeMessage(bridgeVersion, {
        playing: following || playback.playing,
        speed: playback.speed,
        tick,
        tickCount: playback.tickCount,
        atEnd: !following && playback.atEnd,
        following,
        loading: !assets.ready,
        pendingAssets: assets.pending,
      }),
    );
  }, [
    assets.pending,
    assets.ready,
    bridgeVersion,
    following,
    playback.atEnd,
    playback.playing,
    playback.speed,
    playback.tickCount,
    post,
    tick,
  ]);

  return (
    <div className="relative h-screen w-screen bg-arena-bg">
      <ArenaCanvas
        replay={replay}
        time={time}
        selectedUnitKey={selectedUnitKey}
        showVisibility={showVisibility}
        onSelectUnit={(unitKey) => {
          setSelectedUnitKey(unitKey);
          post(selectedBridgeMessage(bridgeVersion, replay, unitKey));
        }}
      />
    </div>
  );
}
