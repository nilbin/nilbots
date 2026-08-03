import { useEffect, useRef } from 'react';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { drawArena } from '../render/drawArena';
import { posesAt } from '../render/interpolate';
import {
  ArenaCamera,
  arenaViewport,
  directorMinSpan,
  directorShotHoldTicks,
  focusFrame,
  focusPointsAt,
  strategicOverviewFrame,
  type ArenaFraming,
} from '../render/arenaCamera';
import { attachCameraGestures } from '../render/cameraGestures';
import {
  logicalArenaHeight,
  unprojectCanvasY,
} from '../render/arenaProjection';
import type { ViewerEntrantPresentation } from './Viewer';

interface ArenaCanvasProps {
  replay: ReplayModel;
  time: number;
  selectedUnitKey: ReplayStableUnitKey | null;
  highlightedUnitKeys?: readonly ReplayStableUnitKey[];
  showVisibility: boolean;
  onSelectUnit: (unitKey: ReplayStableUnitKey | null) => void;
  /** Follow the action. On by default; a gesture or the chrome's toggle turns it off. */
  autoFit?: boolean;
  /** Fired when a gesture takes the camera, so the chrome can show auto-fit as off. */
  onManualCamera?: () => void;
  /**
   * Whether this surface offers pan and zoom at all.
   *
   * Off in the mobile app's WebView. There the host draws the transport natively and the
   * page has no chrome of its own, so a gesture that dropped auto-fit would have nothing to
   * turn it back on — the camera would silently stop following for the rest of the match.
   * Auto-fit still runs there; only the override is withheld, because a bridge that carries
   * no gesture also carries no way to undo one.
   */
  cameraGestures?: boolean;
  entrants?: readonly ViewerEntrantPresentation[];
}

export default function ArenaCanvas({
  replay,
  time,
  selectedUnitKey,
  highlightedUnitKeys = [],
  showVisibility,
  onSelectUnit,
  autoFit = true,
  onManualCamera,
  cameraGestures = true,
  entrants,
}: ArenaCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const stateRef = useRef({
    time,
    selectedUnitKey,
    highlightedUnitKeys,
    showVisibility,
    autoFit,
  });
  stateRef.current = {
    time,
    selectedUnitKey,
    highlightedUnitKeys,
    showVisibility,
    autoFit,
  };
  const overrideRef = useRef(onManualCamera);
  overrideRef.current = onManualCamera;
  // Survives re-renders and belongs to the replay, not the frame: the spring's whole job
  // is to remember where it was going.
  const cameraRef = useRef<ArenaCamera | null>(null);
  const framingRef = useRef<ArenaFraming>({
    mapWidth: replay.map.width,
    mapHeight: replay.map.height,
    aspect: 1,
    minSpan: directorMinSpan(replay),
  });

  const gesturesRef = useRef<{ panned: () => boolean } | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current!;
    const ctx = canvas.getContext('2d')!;
    let frame = 0;
    let last: number | null = null;
    let aspect = 0;
    let lastFit = stateRef.current.autoFit;
    // A new replay is a new match: the camera must not open framed on the last one's fight.
    cameraRef.current = null;

    const framing = () => framingRef.current;
    const scale = () =>
      arenaViewport(
        cameraRef.current?.frame ?? null,
        replay.map.width,
        replay.map.height,
        canvas.clientWidth || 1,
        logicalArenaHeight(canvas.clientHeight || 1),
      ).tile;

    const gestures = cameraGestures
      ? attachCameraGestures({
          element: canvas,
          // Created below on the first frame, which is the first moment the viewport has a
          // size — so the gesture host reads it lazily rather than capturing it.
          get camera() {
            return cameraRef.current!;
          },
          framing,
          scale,
          onOverride: () => overrideRef.current?.(),
        })
      : null;
    gesturesRef.current = gestures;

    const render = (stamp: number) => {
      const parent = canvas.parentElement!;
      const ratio = window.devicePixelRatio || 1;
      const width = parent.clientWidth;
      const height = parent.clientHeight;
      if (canvas.width !== width * ratio || canvas.height !== height * ratio) {
        canvas.width = width * ratio;
        canvas.height = height * ratio;
        canvas.style.width = `${width}px`;
        canvas.style.height = `${height}px`;
      }
      ctx.setTransform(ratio, 0, 0, ratio, 0, 0);

      const {
        time: now,
        selectedUnitKey: followed,
        highlightedUnitKeys: highlighted,
        showVisibility: fov,
        autoFit: fit,
      } =
        stateRef.current;
      if (width > 0 && height > 0) {
        framingRef.current = {
          mapWidth: replay.map.width,
          mapHeight: replay.map.height,
          aspect: width / logicalArenaHeight(height),
          minSpan: directorMinSpan(replay),
        };
        const camera =
          cameraRef.current ?? (cameraRef.current = new ArenaCamera(framingRef.current));
        // A resized or rotated viewport re-shapes the frame rather than re-deciding it.
        if (aspect !== 0 && Math.abs(aspect - framingRef.current.aspect) > 1e-6)
          camera.reframe(framingRef.current);
        aspect = framingRef.current.aspect;

        // On the toggle *changing*, never on a mismatch. A gesture drops auto-fit inside
        // the camera immediately and reports it upward, and React delivers that state a
        // frame or two later — so a camera that reacted to the mismatch would spend those
        // frames re-engaging the fit and undoing the gesture that had just been made.
        if (fit !== lastFit) {
          lastFit = fit;
          if (fit) camera.engage();
          else camera.showFrame(strategicOverviewFrame(replay, framingRef.current));
        }
        if (camera.auto)
          camera.aim(
            focusFrame(
              focusPointsAt(replay, now, followed, highlighted),
              framingRef.current,
            ),
            now,
            followed === null ? directorShotHoldTicks(replay) : 0,
          );
        // Real seconds, so the camera settles at the same rate whatever the playback speed
        // and whatever the frame rate; the spring clamps a huge step of its own accord.
        camera.advance(last === null ? 0 : (stamp - last) / 1000);
        last = stamp;
      }

      drawArena(
        ctx,
        replay,
        {
          time: now,
          selectedUnitKey: followed,
          highlightedUnitKeys: highlighted,
          showVisibility: fov,
          frame: cameraRef.current?.frame ?? null,
          entrants,
        },
        width,
        height,
      );
      frame = requestAnimationFrame(render);
    };
    frame = requestAnimationFrame(render);
    return () => {
      cancelAnimationFrame(frame);
      gestures?.detach();
      gesturesRef.current = null;
    };
  }, [replay, cameraGestures, entrants]);

  const handleClick = (event: React.MouseEvent<HTMLCanvasElement>) => {
    // A drag that moved the camera is not a tap on a bot.
    if (gesturesRef.current?.panned()) return;
    // Hit-test bots at their current interpolated tile, through the camera's own transform
    // — a zoomed-in arena must still pick the bot that is under the cursor.
    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    const clickX = event.clientX - rect.left;
    const clickY = event.clientY - rect.top;
    const { tile, originX, originY } = arenaViewport(
      cameraRef.current?.frame ?? null,
      replay.map.width,
      replay.map.height,
      rect.width,
      logicalArenaHeight(rect.height),
    );

    for (const pose of posesAt(replay, stateRef.current.time)) {
      const cx = originX + pose.x * tile + tile / 2;
      const cy = originY + pose.y * tile + tile / 2;
      if (
        Math.hypot(clickX - cx, unprojectCanvasY(clickY) - cy) <
        tile * 0.5
      ) {
        onSelectUnit(
          selectedUnitKey === pose.unitKey ? null : pose.unitKey,
        );
        return;
      }
    }
    onSelectUnit(null);
  };

  return (
    <canvas
      ref={canvasRef}
      onClick={handleClick}
      // Absolutely positioned, not in flow. The render loop sizes this canvas from
      // its parent every frame, so if the canvas also contributed to the parent's
      // height the two would chase each other: canvas grows, parent grows by its
      // border, canvas reads the larger value next frame. That is the arena slowly
      // inflating on load. Out of flow, the parent measures independently.
      className="absolute inset-0 h-full w-full cursor-pointer"
      role="img"
      aria-label="nilbots match playback"
    />
  );
}
