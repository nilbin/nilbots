import { useEffect, useRef } from 'react';
import type { ReplayDocument } from '../types';
import { drawArena } from '../render/drawArena';

interface ArenaCanvasProps {
  replay: ReplayDocument;
  time: number;
  selectedSlot: number | null;
  showVisibility: boolean;
  onSelectSlot: (slot: number | null) => void;
}

export default function ArenaCanvas({
  replay,
  time,
  selectedSlot,
  showVisibility,
  onSelectSlot,
}: ArenaCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const stateRef = useRef({ time, selectedSlot, showVisibility });
  stateRef.current = { time, selectedSlot, showVisibility };

  useEffect(() => {
    const canvas = canvasRef.current!;
    const ctx = canvas.getContext('2d')!;
    let frame = 0;
    const render = () => {
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
      drawArena(ctx, replay, stateRef.current, width, height);
      frame = requestAnimationFrame(render);
    };
    frame = requestAnimationFrame(render);
    return () => cancelAnimationFrame(frame);
  }, [replay]);

  const handleClick = (event: React.MouseEvent<HTMLCanvasElement>) => {
    // Hit-test bots at their current interpolated tile.
    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    const clickX = event.clientX - rect.left;
    const clickY = event.clientY - rect.top;
    const { mapWidth, mapHeight } = replay.header;
    const tile = Math.floor(
      // Mirrors drawArena's MARGIN_TILES; if they disagree, clicks land on the wrong bot.
      Math.min(rect.width / (mapWidth + 0.4), rect.height / (mapHeight + 0.4)),
    );
    const originX = Math.floor((rect.width - tile * mapWidth) / 2);
    const originY = Math.floor((rect.height - tile * mapHeight) / 2);

    const tickCount = replay.ticks.length;
    const tick = Math.min(Math.floor(stateRef.current.time), tickCount - 1);
    const states = tick <= 0
      ? replay.header.participants.map((p) => ({ slot: p.slot, x: p.spawnX, y: p.spawnY }))
      : replay.ticks[tick - 1 >= 0 ? tick - 1 : 0].state;

    for (const state of states) {
      const cx = originX + state.x * tile + tile / 2;
      const cy = originY + state.y * tile + tile / 2;
      if (Math.hypot(clickX - cx, clickY - cy) < tile * 0.5) {
        onSelectSlot(selectedSlot === state.slot ? null : state.slot);
        return;
      }
    }
    onSelectSlot(null);
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
