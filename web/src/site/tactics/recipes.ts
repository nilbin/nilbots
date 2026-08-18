import { ZONE_OPTIONS } from './places';
import {
  freshLocalId, identifierFromName,
  type ActKind, type CoreRule, type DeliveryPlan, type MaxAttempt, type Posture,
  type Rect, type RecipeId, type StageKind, type Tactic, type TacticConds,
  type TacticGroup, type TacticOutcomes, type TacticsDocument,
} from './tacticsDocument';

/**
 * Recipes prefill the one universal Tactic structure — groups, phases,
 * release, outcomes. Geometry is the player's; hints suggest where to draw.
 * The recipe id is provenance only: any later pruning or suggestion keys off
 * document content, never off which recipe created it.
 */

export interface RecipeGroupPrefill {
  name: string;
  size: number | null;
  core: CoreRule;
  stage: { kind: StageKind; posture: Posture };
  act: { kind: ActKind; posture: Posture };
  hint: { rect: Rect; text: string } | null;
}

export interface Recipe {
  id: RecipeId;
  label: string;
  icon: string;
  tagline: string;
  groups: RecipeGroupPrefill[];
  start?: { conds: TacticConds };
  release: { mode: 'immediate' | 'ready' | 'ready-when'; conds?: TacticConds };
  outcomes: TacticOutcomes;
  delivery?: DeliveryPlan;
  maxAttempt: MaxAttempt;
}

const MID = ZONE_OPTIONS.find((zone) => zone.id === 'mid')!;

export const RECIPES: Recipe[] = [
  {
    id: 'hold-area',
    label: 'Hold Area',
    icon: '▣',
    tagline: 'Occupy ground and keep it. Ambushes and defences are this with different geometry.',
    groups: [
      {
        name: 'Holders', size: 8, core: 'ignore',
        stage: { kind: 'posts', posture: 'hold' },
        act: { kind: 'none', posture: 'hold' },
        hint: { rect: [12, 10, 18, 16], text: 'Post up on the ground you intend to keep.' },
      },
    ],
    release: { mode: 'immediate' },
    outcomes: { cleared: { zone: { name: MID.id, rect: MID.rect } }, persistent: true },
    maxAttempt: 'none',
  },
  {
    id: 'centre-rush',
    label: 'Centre Rush',
    icon: '⇥',
    tagline: 'Gather, then punch straight down the middle together.',
    groups: [
      {
        name: 'Rush Party', size: 8, core: 'grab',
        stage: { kind: 'area', posture: 'wait' },
        act: { kind: 'advance', posture: 'aggressive' },
        hint: { rect: [8, 10, 13, 15], text: 'Assemble on your side of Mid, then draw the push.' },
      },
    ],
    release: { mode: 'ready' },
    outcomes: { kills: { count: 3 }, coreAcquired: true, reached: true },
    maxAttempt: 'standard',
  },
  {
    id: 'pincer',
    label: 'Pincer',
    icon: '⋔',
    tagline: 'A small guard baits at Mid while the rest close from behind.',
    groups: [
      {
        name: 'Mid Guard', size: 2, core: 'ignore',
        stage: { kind: 'posts', posture: 'hold' },
        act: { kind: 'none', posture: 'hold' },
        hint: { rect: [13, 11, 17, 15], text: 'Two posts at Mid — visible enough to be worth attacking.' },
      },
      {
        name: 'Flankers', size: 6, core: 'grab',
        stage: { kind: 'area', posture: 'wait' },
        act: { kind: 'converge', posture: 'aggressive' },
        hint: { rect: [21, 3, 27, 8], text: 'Stage behind their lines, out of the fight.' },
      },
    ],
    release: {
      mode: 'ready-when',
      conds: { enemiesAt: { count: 2, zone: { name: MID.id, rect: MID.rect } } },
    },
    outcomes: {
      kills: { count: 3 },
      cleared: { zone: { name: MID.id, rect: MID.rect } },
      coreAcquired: true,
    },
    maxAttempt: 'standard',
  },
  {
    id: 'core-raid',
    label: 'Core Raid',
    icon: '◉',
    tagline: 'Runners take a Well while a screen keeps the fight off them.',
    groups: [
      {
        name: 'Raiders', size: 2, core: 'hunt',
        stage: { kind: 'approach', posture: 'steal' },
        act: { kind: 'return', posture: 'steal' },
        hint: { rect: [17, 18, 21, 22], text: 'Approach short of their Well.' },
      },
      {
        name: 'Screen', size: 6, core: 'ignore',
        stage: { kind: 'area', posture: 'hold' },
        act: { kind: 'none', posture: 'hold' },
        hint: { rect: [13, 14, 18, 19], text: 'Screen between the Well and their spawns.' },
      },
    ],
    release: { mode: 'ready-when', conds: { coreLoose: { well: 'enemy' } } },
    outcomes: { coreAcquired: true, coreBanked: true, coreLost: true },
    delivery: 'solo',
    maxAttempt: 'standard',
  },
  {
    id: 'patrol',
    label: 'Patrol',
    icon: '↻',
    tagline: 'Walk a beat and keep eyes on the ground between fights.',
    groups: [
      {
        name: 'Patrollers', size: 8, core: 'ignore',
        stage: { kind: 'none', posture: 'observe' },
        act: { kind: 'patrol', posture: 'observe' },
        hint: { rect: [6, 8, 24, 18], text: 'Draw the loop you want walked.' },
      },
    ],
    release: { mode: 'immediate' },
    outcomes: { persistent: true },
    maxAttempt: 'none',
  },
  {
    id: 'deliver-core',
    label: 'Deliver Core',
    icon: '⌂',
    tagline: 'Bring a held Core home. The carrier is whoever has it.',
    groups: [
      {
        name: 'Carrier', size: 1, core: 'grab',
        stage: { kind: 'none', posture: 'wait' },
        act: { kind: 'return', posture: 'wait' },
        hint: null,
      },
      {
        name: 'Escorts', size: 3, core: 'ignore',
        stage: { kind: 'none', posture: 'escort' },
        act: { kind: 'escort', posture: 'escort' },
        hint: null,
      },
      {
        name: 'Screen', size: 4, core: 'ignore',
        stage: { kind: 'area', posture: 'hold' },
        act: { kind: 'none', posture: 'hold' },
        hint: { rect: [4, 10, 9, 16], text: 'Screen the lane the carrier will come down.' },
      },
    ],
    start: { conds: { carrier: true } },
    release: { mode: 'immediate' },
    outcomes: { coreBanked: true, coreLost: true },
    delivery: 'escort',
    maxAttempt: 'none',
  },
  {
    id: 'blank',
    label: 'Blank Tactic',
    icon: '＋',
    tagline: 'The same structure with nothing filled in.',
    groups: [
      {
        name: 'Everyone', size: 8, core: 'grab',
        stage: { kind: 'posts', posture: 'hold' },
        act: { kind: 'none', posture: 'hold' },
        hint: null,
      },
    ],
    release: { mode: 'ready' },
    outcomes: {},
    maxAttempt: 'none',
  },
];

export function recipeById(id: RecipeId): Recipe {
  return RECIPES.find((recipe) => recipe.id === id) ?? RECIPES[RECIPES.length - 1];
}

export interface RecipeHints {
  groups: Map<string, { rect: Rect; text: string }>;
  /** Recipe-suggested group sizes; “Distribute the rest” weights by them. */
  targetSizes: Map<string, number | null>;
}

export function instantiateRecipe(
  recipe: Recipe,
  document: TacticsDocument,
): { tactic: Tactic; hints: RecipeHints } {
  const twins = document.tactics.filter((tactic) => tactic.recipe === recipe.id).length;
  const name = twins ? `${recipe.label} ${twins + 1}` : recipe.label;
  const hints: RecipeHints = { groups: new Map(), targetSizes: new Map() };
  const usedIds = new Set(document.tactics.map((tactic) => tactic.id));
  let tacticId = identifierFromName(name, recipe.id);
  while (usedIds.has(tacticId)) tacticId = `${tacticId}-x`.slice(0, 24);
  const groups: TacticGroup[] = recipe.groups.map((prefill, index) => {
    const group = makeGroup(prefill, index);
    if (prefill.hint) hints.groups.set(group.id, prefill.hint);
    hints.targetSizes.set(group.id, prefill.size);
    return group;
  });
  const tactic: Tactic = {
    id: tacticId,
    name,
    recipe: recipe.id,
    start: recipe.start
      ? { mode: 'when', conds: structuredClone(recipe.start.conds) }
      : { mode: 'always' },
    release: recipe.release.conds
      ? { mode: recipe.release.mode, conds: structuredClone(recipe.release.conds) }
      : { mode: recipe.release.mode },
    finish: { outcomes: structuredClone(recipe.outcomes), maxAttempt: recipe.maxAttempt },
    delivery: { plan: recipe.delivery ?? 'none' },
    groups,
  };
  return { tactic, hints };
}

export function makeGroup(prefill: RecipeGroupPrefill, index: number): TacticGroup {
  const aggressiveActs = ['aggressive', 'steal'];
  return {
    id: `${identifierFromName(prefill.name, `group-${index + 1}`)}`,
    name: prefill.name,
    slots: [],
    coreRule: prefill.core,
    stage: { kind: prefill.stage.kind, posture: prefill.stage.posture, recovery: 'patch' },
    act: {
      kind: prefill.act.kind,
      posture: prefill.act.posture,
      recovery: aggressiveActs.includes(prefill.act.posture) ? 'fight-on' : 'patch',
    },
  };
}

export function freshGroup(existing: TacticGroup[]): TacticGroup {
  const name = `Group ${'ABC'[existing.length] ?? existing.length + 1}`;
  const group = makeGroup({
    name, size: null, core: 'grab',
    stage: { kind: 'posts', posture: 'hold' },
    act: { kind: 'none', posture: 'hold' },
    hint: null,
  }, existing.length);
  const used = new Set(existing.map((value) => value.id));
  while (used.has(group.id)) group.id = `${group.id}-x`.slice(0, 24);
  return group;
}

export { freshLocalId };
