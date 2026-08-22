import type { RecipeHints } from './recipes';
import { possessive } from './summary';
import {
  activeCaseCount, effectiveJob, expectedGeometry, jobHasGeometry, jobOf,
  type Tactic, type TacticsDocument,
} from './tacticsDocument';

export type WizardStepId =
  | 'start' | 'groups' | 'phases' | 'cores' | 'review';

/** Ordered like the life of a Tactic: when it applies (and when it bails),
 * who is in it, the phases of the operation, Cores, and the readback. */
export const WIZARD_STEPS: { id: WizardStepId; label: string }[] = [
  { id: 'start', label: 'Start' },
  { id: 'groups', label: 'Groups' },
  { id: 'phases', label: 'Phases' },
  { id: 'cores', label: 'Cores' },
  { id: 'review', label: 'Review' },
];

export interface WizardState {
  tactic: Tactic;
  hints: RecipeHints;
  step: WizardStepId;
  /** The storyboard's selected phase — what the map draws handles for. */
  phaseIndex: number;
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

/** A group's job in one phase has everything its kind needs drawn. */
export function jobComplete(
  tactic: Tactic,
  phaseIndex: number,
  groupId: string,
): boolean {
  const group = tactic.groups.find((value) => value.id === groupId);
  const job = jobOf(tactic, phaseIndex, groupId);
  if (!group || !job) return false;
  if (job.kind === 'none') {
    return effectiveJob(tactic, phaseIndex, groupId) !== null;
  }
  // Train draws nothing, but it is only finished once a Workshop is named
  // and the Group is the single bot the grammar allows.
  if (job.kind === 'train') {
    return Boolean(job.workshop) && group.slots.length === 1;
  }
  const expected = expectedGeometry(job.kind);
  if (expected === null) return true;
  if (expected === 'posts') {
    if (group.slots.length === 0) return false;
    const posts = job.posts ?? [];
    // Slot-keyed posts are complete when every member is covered; posts
    // from before slots existed pair positionally, so count suffices.
    if (posts.some((post) => post.slot != null)) {
      return group.slots.every((slot) =>
        posts.some((post) => post.slot === slot));
    }
    return posts.length === group.slots.length;
  }
  if (expected === 'rect') return Boolean(job.rect);
  if (expected === 'point') return Boolean(job.point);
  return (job.path?.length ?? 0) > 1;
}

export function phaseComplete(tactic: Tactic, phaseIndex: number): boolean {
  const phase = tactic.phases[phaseIndex];
  if (!phase) return false;
  const jobsDone = tactic.groups.every((group) =>
    jobComplete(tactic, phaseIndex, group.id));
  const last = phaseIndex === tactic.phases.length - 1;
  const rules = phase.continue ?? [];
  const exitDone = last
    || rules.some((rule) => activeCaseCount(rule) > 0);
  return jobsDone && exitDone;
}

export function stepDone(state: WizardState, id: WizardStepId): boolean {
  const tactic = state.tactic;
  switch (id) {
    case 'groups':
      return assignedCount(tactic) === 8
        && tactic.groups.every((group) => group.slots.length > 0);
    case 'phases':
      return tactic.phases.length > 0
        && tactic.phases.every((_, index) => phaseComplete(tactic, index))
        && tactic.groups.some((group) => jobHasGeometry(jobOf(tactic, 0, group.id)));
    case 'cores': case 'start':
      return true;
    default:
      return false;
  }
}

/** The one gate on Create: what — in plain words — still stands in the way. */
export function createGate(state: WizardState, document: TacticsDocument): string | null {
  const tactic = state.tactic;
  if (document.squad.length !== 8) return 'Fill all eight squad slots first.';
  if (assignedCount(tactic) !== 8) {
    return `Assign all eight bots to Groups (${assignedCount(tactic)}/8).`;
  }
  if (tactic.groups.some((group) => group.slots.length === 0)) {
    return 'Every Group needs at least one bot — dissolve empty ones.';
  }
  if (!tactic.groups.some((group) => jobHasGeometry(jobOf(tactic, 0, group.id)))) {
    return 'The first phase needs at least one Group with drawn geometry.';
  }
  for (let index = 0; index < tactic.phases.length; index++) {
    if (!phaseComplete(tactic, index)) {
      const phase = tactic.phases[index];
      return `${possessive(phase.name)} phase is not finished — draw its jobs and set its “Continue when…”.`;
    }
  }
  if (tactic.start.mode === 'when'
    && !(tactic.start.cases ?? []).some((value) => activeCaseCount(value) > 0)) {
    return 'Start is “only when…” but no condition is ticked.';
  }
  return null;
}
