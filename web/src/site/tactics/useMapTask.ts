import { useCallback, useState } from 'react';
import type { DrawKind, Rect } from './tacticsDocument';

/**
 * The one draw-task state machine: at most one thing is being drawn on the
 * map at a time — a group job's geometry in one phase, a signature mark set,
 * or a condition watch box. The canvas asks it what a click means; the
 * drawer's buttons start and stop it.
 */

/** Which condition-rows object on the tactic a watch box belongs to. */
export type WatchTarget =
  | {
    scope: 'start';
    caseIndex: number;
    side: 'required' | 'triggers' | 'unless';
    row: 'enemiesAt' | 'cleared' | 'notFighting';
    zoneIndex: number;
  }
  | {
    scope: 'abort';
    caseIndex: number;
    side: 'required' | 'triggers' | 'unless';
    row: 'enemiesAt' | 'cleared' | 'notFighting';
    zoneIndex: number;
  }
  | {
    scope: 'continue';
    phaseIndex: number;
    ruleIndex: number;
    side: 'required' | 'triggers' | 'unless';
    row: 'enemiesAt' | 'cleared' | 'notFighting';
    zoneIndex: number;
  };

export type MapTask =
  | {
    kind: 'job';
    tacticId: string;
    groupId: string;
    phaseIndex: number;
    draw: DrawKind;
    /** Set to 'via' when the route being drawn is an approach route. */
    field?: 'via';
    /** With field 'via': the post whose own way in is being drawn; absent
     * for the group-level route. */
    postIndex?: number;
    hint?: { rect: Rect; text: string } | null;
    needed?: number;
  }
  | {
    kind: 'sig';
    tacticId: string;
    groupId: string;
    phaseIndex: number;
    slot: number;
    classId: string;
    draw: 'posts' | 'rect' | 'route';
    max: number;
  }
  | {
    kind: 'carry';
    tacticId: string;
    phaseIndex: number;
    draw: 'route';
  }
  | {
    kind: 'watch';
    tacticId: string;
    target: WatchTarget;
  };

export function useMapTask() {
  const [task, setTask] = useState<MapTask | null>(null);
  const start = useCallback((next: MapTask) => setTask(next), []);
  const end = useCallback(() => setTask(null), []);
  return { task, start, end };
}
