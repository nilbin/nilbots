import type { ReplayActorState, ReplayForm, ReplayModel } from './replayModel';

/**
 * Normalization has already recovered historical replay-v1 health metadata
 * into its synthetic form, while replay-v2 carries exact public form rules.
 */
export function replayMaxHealth(replay: ReplayModel): number {
  return Math.max(1, ...replay.forms.map((form) => form.maxHealth));
}

export function formForActor(
  replay: ReplayModel,
  actor: Pick<ReplayActorState, 'formId'>,
): ReplayForm | null {
  return replay.forms.find((form) => form.formId === actor.formId) ?? null;
}

export function maxHealthForActor(
  replay: ReplayModel,
  actor: Pick<ReplayActorState, 'formId' | 'health'>,
): number {
  return formForActor(replay, actor)?.maxHealth ?? Math.max(1, actor.health);
}
