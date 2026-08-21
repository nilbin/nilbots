import type { TacticalSheetCatalog } from '../api';
import { suggestFormationPosts } from './mapFirstSpatial';
import { ZONE_OPTIONS } from './places';
import {
  freshLocalId, freshJob, identifierFromName,
  type CoreRule, type DeliveryPlan, type JobKind, type MaxDuration,
  type Point, type Posture, type Rect, type RecipeId, type Tactic, type TacticCase,
  type TacticConds, type TacticGroup, type TacticJob, type TacticPhaseDef,
  type TacticTransition, type TacticsDocument,
} from './tacticsDocument';

/**
 * Recipes prefill the one universal Tactic structure — groups, ordered named
 * phases, transitions. Geometry is the player's; hints suggest where to draw.
 * The recipe id is provenance only: any later pruning or suggestion keys off
 * document content, never off which recipe created it.
 */

export interface RecipeGroupPrefill {
  name: string;
  size: number | null;
  core: CoreRule;
  hint: { rect: Rect; text: string } | null;
}

export interface RecipePhasePrefill {
  name: string;
  /** One job per group, aligned with the groups array; null = carry on. */
  jobs: ({ kind: JobKind; posture: Posture; objective?: Point } | null)[];
  /** Absent on the final phase = the operation holds there forever. */
  continue?: { required?: TacticConds; triggers?: TacticConds }[];
}

export interface Recipe {
  id: RecipeId;
  label: string;
  icon: string;
  tagline: string;
  groups: RecipeGroupPrefill[];
  phases: RecipePhasePrefill[];
  start?: { cases: TacticCase[] };
  delivery?: DeliveryPlan;
  maxDuration?: MaxDuration;
}

const MID = ZONE_OPTIONS.find((zone) => zone.id === 'mid')!;
const MID_ZONE = { name: MID.id, rect: MID.rect };

export const RECIPES: Recipe[] = [
  {
    id: 'hold-area',
    label: 'Hold Area',
    icon: '▣',
    tagline: 'Occupy ground and keep it. Ambushes and defences are this with different geometry.',
    groups: [
      {
        name: 'Holders', size: 8, core: 'ignore',
        hint: { rect: [12, 10, 18, 16], text: 'Post up on the ground you intend to keep.' },
      },
    ],
    phases: [
      { name: 'Hold', jobs: [{ kind: 'posts', posture: 'hold' }] },
    ],
  },
  {
    id: 'centre-rush',
    label: 'Centre Rush',
    icon: '⇥',
    tagline: 'Gather, then punch straight down the middle together.',
    groups: [
      {
        name: 'Rush Party', size: 8, core: 'grab',
        hint: { rect: [8, 10, 13, 15], text: 'Assemble on your side of Mid, then draw the push.' },
      },
    ],
    phases: [
      {
        name: 'Assemble',
        jobs: [{ kind: 'posts', posture: 'observe', objective: [11, 13] }],
        continue: [{ required: { groupsSet: { allowedLate: 0 } } }],
      },
      {
        name: 'Push',
        jobs: [{ kind: 'posts', posture: 'aggressive', objective: [16, 13] }],
        continue: [{
          triggers: { enemiesDown: { count: 3 }, coreAcquired: true },
        }],
      },
    ],
    maxDuration: 'standard',
  },
  {
    id: 'pincer',
    label: 'Pincer',
    icon: '⋔',
    tagline: 'Bait at Mid, close from behind, take the ground, and walk the prize home.',
    groups: [
      {
        name: 'Mid Guard', size: 2, core: 'ignore',
        hint: { rect: [13, 11, 17, 15], text: 'Two posts at Mid — visible enough to be worth attacking.' },
      },
      {
        name: 'Flankers', size: 6, core: 'grab',
        hint: { rect: [21, 3, 27, 8], text: 'Stage behind their lines, out of the fight.' },
      },
    ],
    phases: [
      {
        name: 'Set the trap',
        jobs: [
          { kind: 'posts', posture: 'hold', objective: [15, 13] },
          { kind: 'posts', posture: 'observe', objective: [24, 5] },
        ],
        continue: [{
          required: { groupsSet: { allowedLate: 0 } },
          triggers: { enemiesAt: { count: 5, zones: [{ ...MID_ZONE }] } },
        }],
      },
      {
        name: 'Close the trap',
        jobs: [null, { kind: 'posts', posture: 'aggressive', objective: [16, 13] }],
        continue: [{
          triggers: { enemiesDown: { count: 2 }, cleared: { zones: [{ ...MID_ZONE }] } },
        }],
      },
      {
        name: 'Exploit the opening',
        jobs: [null, { kind: 'posts', posture: 'hold', objective: [19, 13] }],
        continue: [{ triggers: { coreAcquired: true } }],
      },
      {
        name: 'Secure the opening',
        jobs: [null, null],
      },
    ],
    delivery: 'escort',
    maxDuration: 'long',
  },
  {
    id: 'core-raid',
    label: 'Core Raid',
    icon: '◉',
    tagline: 'Approach quietly, take their Well, and run it home under escort.',
    groups: [
      {
        name: 'Raiders', size: 2, core: 'hunt',
        hint: { rect: [17, 18, 21, 22], text: 'Approach short of their Well.' },
      },
      {
        name: 'Screen', size: 6, core: 'ignore',
        hint: { rect: [13, 14, 18, 19], text: 'Screen between the Well and their spawns.' },
      },
    ],
    phases: [
      {
        name: 'Approach',
        jobs: [
          { kind: 'posts', posture: 'observe', objective: [19, 20] },
          { kind: 'posts', posture: 'hold', objective: [16, 17] },
        ],
        continue: [{
          required: { groupsSet: { allowedLate: 0 } },
          triggers: { coreLoose: { well: 'enemy' } },
        }],
      },
      {
        name: 'Steal',
        jobs: [{ kind: 'posts', posture: 'aggressive', objective: [21, 21] }, null],
        continue: [{ triggers: { coreAcquired: true } }],
      },
      {
        name: 'Hold the escape',
        jobs: [null, { kind: 'posts', posture: 'hold', objective: [18, 18] }],
      },
    ],
    delivery: 'solo',
    maxDuration: 'standard',
  },
  {
    id: 'patrol',
    label: 'Patrol',
    icon: '↻',
    tagline: 'Walk a beat and keep eyes on the ground between fights.',
    groups: [
      {
        name: 'Patrollers', size: 8, core: 'ignore',
        hint: { rect: [6, 8, 24, 18], text: 'Draw the loop you want walked.' },
      },
    ],
    phases: [
      { name: 'Walk the beat', jobs: [{ kind: 'patrol', posture: 'observe' }] },
    ],
  },
  {
    id: 'deliver-core',
    label: 'Deliver Core',
    icon: '⌂',
    tagline: 'Bring a held Core home. The carrier is whoever has it.',
    groups: [
      { name: 'Carrier', size: 1, core: 'grab', hint: null },
      { name: 'Escorts', size: 3, core: 'ignore', hint: null },
      {
        name: 'Screen', size: 4, core: 'ignore',
        hint: { rect: [4, 10, 9, 16], text: 'Screen the lane the carrier will come down.' },
      },
    ],
    phases: [
      {
        name: 'Run it home',
        jobs: [
          { kind: 'converge', posture: 'observe' },
          { kind: 'converge', posture: 'hold' },
          { kind: 'area', posture: 'hold' },
        ],
      },
    ],
    delivery: 'escort',
  },
  {
    id: 'blank',
    label: 'Blank Tactic',
    icon: '＋',
    tagline: 'The same structure with nothing filled in.',
    groups: [
      { name: 'Everyone', size: 8, core: 'grab', hint: null },
    ],
    phases: [
      { name: 'Phase 1', jobs: [{ kind: 'posts', posture: 'hold' }] },
    ],
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
  catalog?: TacticalSheetCatalog,
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
  assignRecipeMembership(groups, recipe.groups, document);
  const usedPhaseIds = new Set<string>();
  const phases: TacticPhaseDef[] = recipe.phases.map((prefill, index) => {
    let phaseId = identifierFromName(prefill.name, `phase-${index + 1}`);
    while (usedPhaseIds.has(phaseId)) phaseId = `${phaseId}-x`.slice(0, 24);
    usedPhaseIds.add(phaseId);
    const jobs: Record<string, TacticJob> = {};
    const reserved: Point[] = [];
    groups.forEach((group, groupIndex) => {
      const job = prefill.jobs[groupIndex];
      const created = job
        ? freshJob(job.kind, job.posture)
        : freshJob('none', 'hold');
      if (catalog && job?.kind === 'posts') {
        const hint = recipe.groups[groupIndex]?.hint?.rect;
        const objective: Point | null = job.objective ?? (hint
          ? [Math.round((hint[0] + hint[2]) / 2), Math.round((hint[1] + hint[3]) / 2)]
          : null);
        if (objective) {
          created.posts = suggestFormationPosts(
            catalog, group.slots, objective, reserved,
            `${tacticId}:${phaseId}:${group.id}`,
          );
          reserved.push(...created.posts.map((post) => post.at));
        }
      }
      jobs[group.id] = created;
    });
    const phase: TacticPhaseDef = { id: phaseId, name: prefill.name, jobs };
    if (prefill.continue) {
      phase.continue = prefill.continue.map((rule): TacticTransition =>
        structuredClone(rule));
    }
    return phase;
  });
  const tactic: Tactic = {
    id: tacticId,
    name,
    recipe: recipe.id,
    start: recipe.start
      ? { mode: 'when', cases: structuredClone(recipe.start.cases) }
      : { mode: 'always' },
    delivery: { plan: recipe.delivery ?? 'none' },
    groups,
    phases,
  };
  if (recipe.maxDuration && recipe.maxDuration !== 'none') {
    tactic.maxDuration = recipe.maxDuration;
  }
  return { tactic, hints };
}

/**
 * A recipe already declares its intended split, so opening one should begin
 * with tactics rather than membership bookkeeping. Fixed-size Groups receive
 * real squad slots first, in recipe and squad order. Flexible Groups then
 * divide the honest remainder in recipe order. An incomplete squad therefore
 * stays incomplete — no synthetic slot can leak into validation or runtime.
 */
function assignRecipeMembership(
  groups: TacticGroup[],
  prefills: RecipeGroupPrefill[],
  document: TacticsDocument,
): void {
  const available = [...new Set(document.squad.map((bot) => bot.slot))];
  let cursor = 0;

  groups.forEach((group, index) => {
    const requested = prefills[index]?.size;
    if (requested == null) return;
    group.slots = available.slice(cursor, cursor + Math.max(0, requested));
    cursor += group.slots.length;
  });

  const flexible = groups
    .map((group, index) => ({ group, size: prefills[index]?.size }))
    .filter((entry) => entry.size == null);
  flexible.forEach(({ group }, index) => {
    const remaining = available.length - cursor;
    const flexibleLeft = flexible.length - index;
    const take = Math.ceil(remaining / flexibleLeft);
    group.slots = available.slice(cursor, cursor + take);
    cursor += group.slots.length;
  });
}

export function makeGroup(prefill: RecipeGroupPrefill, index: number): TacticGroup {
  return {
    id: `${identifierFromName(prefill.name, `group-${index + 1}`)}`,
    name: prefill.name,
    slots: [],
    coreRule: prefill.core,
  };
}

export function freshGroup(existing: TacticGroup[]): TacticGroup {
  const name = `Group ${'ABC'[existing.length] ?? existing.length + 1}`;
  const group = makeGroup({ name, size: null, core: 'grab', hint: null }, existing.length);
  const used = new Set(existing.map((value) => value.id));
  while (used.has(group.id)) group.id = `${group.id}-x`.slice(0, 24);
  return group;
}

export { freshLocalId };
