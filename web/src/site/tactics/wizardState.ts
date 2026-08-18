import type { RecipeHints } from './recipes';
import { possessive } from './summary';
import type { Tactic, TacticsDocument } from './tacticsDocument';

export type WizardStepId =
  | 'start' | 'groups' | 'stage' | 'release' | 'act' | 'cores' | 'finish' | 'review';

/** Ordered like the life of a Tactic: when it applies, who is in it, where
 * they set up, when they go, what they do after, Cores, and when it is done. */
export const WIZARD_STEPS: { id: WizardStepId; label: string }[] = [
  { id: 'start', label: 'Start' },
  { id: 'groups', label: 'Groups' },
  { id: 'stage', label: 'Stage' },
  { id: 'release', label: 'Release' },
  { id: 'act', label: 'Act' },
  { id: 'cores', label: 'Cores' },
  { id: 'finish', label: 'Finish' },
  { id: 'review', label: 'Review' },
];

export interface WizardState {
  tactic: Tactic;
  hints: RecipeHints;
  step: WizardStepId;
  editing: boolean;
  originalId: string | null;
  armedSlot: number | null;
}

export function assignedCount(tactic: Tactic): number {
  return tactic.groups.reduce((sum, group) => sum + group.slots.length, 0);
}

export function unassignedSlots(tactic: Tactic, document: TacticsDocument): number[] {
  const taken = new Set(tactic.groups.flatMap((group) => group.slots));
  return document.squad.map((bot) => bot.slot).filter((slot) => !taken.has(slot));
}

export function stageComplete(tactic: Tactic, groupId: string): boolean {
  const group = tactic.groups.find((value) => value.id === groupId);
  if (!group) return false;
  if (group.stage.kind === 'none') return true;
  if (group.stage.kind === 'posts') {
    return (group.stage.posts?.length ?? 0) === group.slots.length && group.slots.length > 0;
  }
  return Boolean(group.stage.rect || (group.stage.path?.length ?? 0) > 1 || group.stage.point);
}

export function actComplete(tactic: Tactic, groupId: string): boolean {
  const group = tactic.groups.find((value) => value.id === groupId);
  if (!group) return false;
  const kind = group.act.kind;
  if (kind === 'none' || kind === 'return' || kind === 'escort') return true;
  if (kind === 'converge') return Boolean(group.act.point);
  return (group.act.path?.length ?? 0) > 1;
}

export function stepDone(state: WizardState, id: WizardStepId): boolean {
  const tactic = state.tactic;
  switch (id) {
    case 'groups':
      return assignedCount(tactic) === 8
        && tactic.groups.every((group) => group.slots.length > 0);
    case 'stage':
      return tactic.groups.every((group) => stageComplete(tactic, group.id));
    case 'act':
      return tactic.groups.every((group) => actComplete(tactic, group.id));
    case 'finish':
      return Object.keys(tactic.finish.outcomes).length > 0
        || tactic.finish.maxAttempt !== 'none';
    case 'cores': case 'start': case 'release':
      return true;
    default:
      return false;
  }
}

/** The one gate on Create: what — in plain words — still stands in the way. */
export function createGate(state: WizardState, document: TacticsDocument): string | null {
  const tactic = state.tactic;
  if (document.squad.length !== 8) {
    return 'Fill all eight squad slots first — a Tactic commands the whole team.';
  }
  if (assignedCount(tactic) < 8) {
    return `Assign all eight bots (${assignedCount(tactic)}/8 assigned).`;
  }
  const empty = tactic.groups.find((group) => group.slots.length === 0);
  if (empty) return `${empty.name} has no bots — every Group needs at least one.`;
  const unstaged = tactic.groups.find((group) => !stageComplete(tactic, group.id));
  if (unstaged) return `${unstaged.name} needs its Stage drawn on the map.`;
  const unacted = tactic.groups.find((group) => !actComplete(tactic, group.id));
  if (unacted) {
    return `${possessive(unacted.name)} Act needs its ${unacted.act.kind === 'converge' ? 'destination' : 'route'} drawn.`;
  }
  if (Object.keys(tactic.finish.outcomes).length === 0
    && tactic.finish.maxAttempt === 'none') {
    return 'Pick at least one Outcome (or a time limit) so the Tactic can finish.';
  }
  return null;
}
