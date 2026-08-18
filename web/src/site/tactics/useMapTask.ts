import { useCallback, useState } from 'react';
import type { DrawKind, Rect } from './tacticsDocument';

/**
 * The one draw-task state machine: at most one thing is being drawn on the
 * map at a time — a group phase's geometry, a signature mark set, or a
 * condition watch box. The canvas asks it what a click means; the drawer's
 * buttons start and stop it.
 */

export type MapTask =
  | {
    kind: 'phase';
    tacticId: string;
    groupId: string;
    phase: 'stage' | 'act';
    draw: DrawKind;
    hint?: { rect: Rect; text: string } | null;
    needed?: number;
  }
  | {
    kind: 'sig';
    tacticId: string;
    groupId: string;
    phase: 'stage' | 'act';
    classId: string;
    draw: 'posts' | 'rect' | 'route';
    max: number;
  }
  | {
    kind: 'watch';
    tacticId: string;
    which: 'start' | 'release';
  };

export function useMapTask() {
  const [task, setTask] = useState<MapTask | null>(null);
  const start = useCallback((next: MapTask) => setTask(next), []);
  const end = useCallback(() => setTask(null), []);
  return { task, start, end };
}
