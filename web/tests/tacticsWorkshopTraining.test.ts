import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  ABORT_ROWS, START_ROWS, TRANSITION_ROWS,
  activeCaseCount, activeCondCount, freshJob, jobHasGeometry,
  normalizeDocument, planJobKindChange, stringifyDocument, validateDocument,
  type Tactic, type TacticConds, type TacticsDocument,
} from '../src/site/tactics/tacticsDocument';
import { instantiateRecipe, RECIPES } from '../src/site/tactics/recipes';
import { summarize } from '../src/site/tactics/summary';
import { jobComplete } from '../src/site/tactics/wizardState';
import {
  parseTrainingIntent, trainingInterruptionPhrase, trainingRead,
} from '../src/site/tactics/practice';
import { workshopTrainingTile } from '../src/site/tactics/botProjection';
import {
  isLegalGround, makeBroadloomCatalog, makeBroadloomDocument, withClasses,
} from './support/broadloomCatalog';

/**
 * Train at Workshop and the six Workshop/veterancy condition rows, mirrored
 * from `ArcRelayTacticsDsl.cs`. The server compile is the authority; these
 * pin that the editor refuses exactly what the compiler refuses, so a player
 * never draws a sheet that dies on save.
 */

const catalog = makeBroadloomCatalog();

/** A Tactic with a guard Group holding proven mid-west posts and a Group of
 * one training at a Workshop — the marquee shape from the probe sheets. */
function trainingTactic(document: TacticsDocument): Tactic {
  // The probe sheets' own mid-west band: (14,13) looks north because (15,13)
  // is one of the two mid pillars.
  const posts: { at: [number, number]; facing: 'n' | 'e' }[] = [
    { at: [12, 12], facing: 'e' }, { at: [12, 15], facing: 'e' },
    { at: [13, 13], facing: 'e' }, { at: [11, 12], facing: 'e' },
    { at: [12, 11], facing: 'e' }, { at: [14, 12], facing: 'e' },
    { at: [14, 13], facing: 'n' },
  ];
  return {
    id: 'hold-and-train',
    name: 'Hold Mid, train north',
    recipe: 'hold-area',
    start: { mode: 'always' },
    delivery: { plan: 'none' },
    groups: [
      {
        id: 'holders', name: 'Holders', slots: [0, 1, 3, 4, 5, 6, 7],
        coreRule: 'grab',
      },
      { id: 'trainee', name: 'Workshop trainee', slots: [2], coreRule: 'ignore' },
    ],
    phases: [{
      id: 'hold-and-train',
      name: 'Hold Mid, train north',
      jobs: {
        holders: {
          kind: 'posts', posture: 'hold', recovery: 'heal-tile',
          posts: [0, 1, 3, 4, 5, 6, 7].map((slot, index) => ({
            at: posts[index].at, facing: posts[index].facing, slot,
          })),
        },
        trainee: {
          kind: 'train', posture: 'wait', travel: 'observe',
          recovery: 'fight-on', workshop: 'north',
        },
      },
    }],
  };
}

function errorsFor(tactic: Tactic): string[] {
  const document = makeBroadloomDocument(catalog);
  document.tactics.push(tactic);
  return validateDocument(document, catalog)
    .filter((issue) => issue.severity === 'error')
    .map((issue) => issue.message);
}

test('a one-bot train job validates, counts as ground, and round-trips', () => {
  const document = makeBroadloomDocument(catalog);
  const tactic = trainingTactic(document);
  document.tactics.push(tactic);
  assert.deepEqual(
    validateDocument(document, catalog).filter((issue) => issue.severity === 'error'),
    []);
  // The rules-owned tile is ground even though nothing was drawn, exactly as
  // the server's HasGeometry reads it — so a train-only phase is legal.
  assert.equal(jobHasGeometry(tactic.phases[0].jobs.trainee), true);
  assert.equal(jobComplete(tactic, 0, 'trainee'), true);
  const reopened = JSON.parse(stringifyDocument(document)) as TacticsDocument;
  assert.equal(
    reopened.tactics[0].phases[0].jobs.trainee.workshop, 'north');
});

test('training refuses a crowd, a fighting posture, marks and drawn ground', () => {
  const crowded = trainingTactic(makeBroadloomDocument(catalog));
  crowded.groups[0].slots = [0, 1, 3, 4, 5];
  crowded.groups[1].slots = [2, 6, 7];
  crowded.phases[0].jobs.holders.posts =
    crowded.phases[0].jobs.holders.posts!.slice(0, 5)
      .map((post, index) => ({ ...post, slot: [0, 1, 3, 4, 5][index] }));
  assert.ok(errorsFor(crowded).some((message) =>
    /Training is one bot’s job/.test(message)));

  const fighting = trainingTactic(makeBroadloomDocument(catalog));
  fighting.phases[0].jobs.trainee.posture = 'aggressive';
  assert.ok(errorsFor(fighting).some((message) =>
    /Training is passive/.test(message)));

  const marked = trainingTactic(makeBroadloomDocument(catalog));
  marked.phases[0].jobs.trainee.sigs = { 2: { policy: 'smart' } };
  assert.ok(errorsFor(marked).some((message) =>
    /A trainee casts nothing/.test(message)));

  const drawn = trainingTactic(makeBroadloomDocument(catalog));
  drawn.phases[0].jobs.trainee.point = [16, 13];
  assert.ok(errorsFor(drawn).some((message) =>
    /draws nothing/.test(message)));

  const routed = trainingTactic(makeBroadloomDocument(catalog));
  routed.phases[0].jobs.trainee.via = [[12, 11], [16, 8]];
  assert.ok(errorsFor(routed).some((message) =>
    /does not take an approach route/.test(message)));
});

test('a train job needs its Workshop, and no other job may name one', () => {
  const missing = trainingTactic(makeBroadloomDocument(catalog));
  delete missing.phases[0].jobs.trainee.workshop;
  assert.ok(errorsFor(missing).some((message) =>
    /needs a Workshop chosen/.test(message)));
  assert.equal(jobHasGeometry(missing.phases[0].jobs.trainee), false);

  const wrongKind = trainingTactic(makeBroadloomDocument(catalog));
  wrongKind.phases[0].jobs.holders.workshop = 'south';
  assert.ok(errorsFor(wrongKind).some((message) =>
    /Only a Train at Workshop job names a Workshop/.test(message)));

  const unknown = trainingTactic(makeBroadloomDocument(catalog));
  unknown.phases[0].jobs.trainee.workshop = 'east' as 'north';
  assert.ok(errorsFor(unknown).some((message) =>
    /is not a Workshop/.test(message)));
});

test('switching a job to and from Train keeps the grammar satisfied', () => {
  const posted = freshJob('posts', 'hold');
  posted.posts = [{ at: [12, 12], facing: 'e', slot: 0 }];
  posted.sigs = { 0: { policy: 'only', points: [[13, 13]] } };
  const { next, removed } = planJobKindChange(posted, 'train');
  assert.equal(next.kind, 'train');
  assert.equal(next.posture, 'wait');
  assert.equal(next.workshop, 'either');
  assert.equal(next.posts, undefined);
  assert.equal(next.sigs, undefined);
  assert.ok(removed.some((entry) => /signature marks/.test(entry)));
  // Leaving Train takes the Workshop with it — the server refuses it there.
  const back = planJobKindChange(next, 'patrol');
  assert.equal(back.next.workshop, undefined);
});

test('the five Workshop rows and veteran condition are legal in their server contexts', () => {
  const readAnywhere = [
    'workshopReady', 'workshopTraining', 'workshopContested', 'workshopDormant',
  ] as const;
  for (const row of readAnywhere) {
    assert.ok(START_ROWS.includes(row), `${row} starts a Tactic`);
    assert.ok(TRANSITION_ROWS.includes(row), `${row} continues a phase`);
  }
  // Only the two "something is wrong in that room" rows bail out.
  assert.deepEqual(
    readAnywhere.filter((row) => ABORT_ROWS.includes(row)),
    ['workshopContested', 'workshopDormant']);
  // Phase-anchored outcomes read only where a running phase exists.
  for (const row of ['workshopDone', 'veterancy'] as const) {
    assert.ok(TRANSITION_ROWS.includes(row));
    assert.ok(!START_ROWS.includes(row), `${row} needs a running phase`);
    assert.ok(!ABORT_ROWS.includes(row), `${row} needs a running phase`);
  }
});

test('an illegal Workshop row for its context is named, not silently dropped', () => {
  const tactic = trainingTactic(makeBroadloomDocument(catalog));
  tactic.start = {
    mode: 'when',
    cases: [{ required: { workshopDone: { workshop: 'north', count: 2 } } }],
  };
  assert.ok(errorsFor(tactic).some((message) =>
    /'workshopDone' is not a condition this step takes/.test(message)));

  const aborting = trainingTactic(makeBroadloomDocument(catalog));
  aborting.abort = {
    cases: [{ required: { veterancy: { slots: [2], count: 1, level: 4 } } }],
  };
  assert.ok(errorsFor(aborting).some((message) =>
    /'veterancy' is not a condition this step takes/.test(message)));
});

test('Workshop row values are bounded exactly as the server bounds them', () => {
  const withRows = (rows: TacticConds): string[] => {
    const tactic = trainingTactic(makeBroadloomDocument(catalog));
    tactic.phases.push({
      id: 'after', name: 'After', jobs: {
        holders: { kind: 'none', posture: 'hold', recovery: 'fight-on' },
        trainee: { kind: 'none', posture: 'wait', recovery: 'fight-on' },
      },
    });
    tactic.phases[0].continue = [{ required: rows }];
    return errorsFor(tactic);
  };
  assert.deepEqual(
    withRows({ workshopDone: { workshop: 'north', count: 3 } }), []);
  assert.deepEqual(
    withRows({ veterancy: { slots: [7], count: 1, level: 5 } }), []);
  assert.deepEqual(
    withRows({ workshopTraining: { workshop: 'either', team: 'enemy' } }), []);
  assert.ok(withRows({ workshopDone: { workshop: 'north', count: 9 } })
    .some((message) => /from one to eight/.test(message)));
  assert.ok(withRows({ veterancy: { slots: [8], count: 1, level: 4 } })
    .some((message) => /distinct squad bots/.test(message)));
  assert.ok(withRows({ veterancy: { slots: [0], count: 1, level: 1 } })
    .some((message) => /from 2 to 5/.test(message)));
  assert.ok(withRows({
    workshopTraining: { workshop: 'north', team: 'neutral' as 'any' },
  }).some((message) => /is not a side/.test(message)));
  assert.ok(withRows({ workshopReady: { workshop: 'middle' as 'north' } })
    .some((message) => /names north, south, or either/.test(message)));
});

test('Workshop rows count as active conditions on both sides of a case', () => {
  assert.equal(activeCondCount({ workshopContested: { workshop: 'north' } }), 1);
  assert.equal(activeCaseCount({
    required: { workshopReady: { workshop: 'north' } },
    triggers: { veterancy: { slots: [1], count: 1, level: 3 } },
  }), 2);
  // A case whose only row is a Workshop row is a real case, not an empty one.
  const tactic = trainingTactic(makeBroadloomDocument(catalog));
  tactic.start = {
    mode: 'when',
    cases: [{ required: { workshopReady: { workshop: 'either' } } }],
  };
  assert.deepEqual(errorsFor(tactic), []);
});

test('the readback names the trainee, the room, and every Workshop row', () => {
  const document = makeBroadloomDocument(catalog);
  const tactic = trainingTactic(document);
  document.squad[2].name = 'Gantry';
  const sentence = summarize(tactic, (slot) => document.squad[slot]?.name ?? null);
  assert.match(sentence, /Gantry trains at the north Workshop/);

  const rows = trainingTactic(document);
  rows.phases.push({
    id: 'answer', name: 'Answer the Workshop', jobs: {
      holders: { kind: 'none', posture: 'aggressive', recovery: 'fight-on' },
      trainee: { kind: 'none', posture: 'wait', recovery: 'fight-on' },
    },
  });
  rows.phases[0].continue = [{
    required: {
      workshopTraining: { workshop: 'north', team: 'enemy' },
      workshopDone: { workshop: 'north', count: 2 },
      veterancy: { slots: [2], count: 1, level: 4 },
    },
    unless: { workshopContested: { workshop: 'north' } },
  }];
  const answered = summarize(rows, (slot) => document.squad[slot]?.name ?? null);
  assert.match(answered, /an enemy is training at the north Workshop/);
  assert.match(answered, /we have finished two trainings at the north Workshop this phase/);
  assert.match(answered, /1 of 1 selected bot reaches level 4/);
  assert.match(answered, /unless enemies are standing in the north Workshop/);
});

test('the practice readback says what the trainee is doing and why it stalled', () => {
  const walking = 'orders:t2/p1 gantry > t2 > t2-p1 > t2-p1-g2-move'
    + ':march@18,4:urgency-normal:move:workshop:north';
  const standing = 'orders:t2/p1 gantry > t2 > t2-p1 > t2-p1-g2-move'
    + ':march@18,4:urgency-normal:at-target:workshop:north';
  assert.deepEqual(parseTrainingIntent(walking),
    { workshopId: 'north', onTile: false });
  assert.deepEqual(parseTrainingIntent(standing),
    { workshopId: 'north', onTile: true });
  assert.equal(parseTrainingIntent(
    'orders:t1/p1 anvil > t1 > t1-p1 > t1-p1-g1-move:march@12,12:at-target:posts:g1'),
    null);

  assert.match(trainingRead(walking)!.line, /Walking to the north Workshop\./);
  const contested = trainingRead(standing, {
    teamId: 0,
    workshops: [{
      workshopId: 'north', phase: 'contested', trainingTeamId: null,
      progressTicks: 0, requiredTicks: 60,
    }],
  })!;
  assert.equal(contested.stalled, true);
  assert.match(contested.line, /enemies in the room have paused it/);

  const dormant = trainingRead(standing, {
    workshops: [{
      workshopId: 'north', phase: 'dormant', trainingTeamId: null,
      progressTicks: 0, requiredTicks: 60,
    }],
  })!;
  assert.match(dormant.line, /the room is recharging/);

  const ours = trainingRead(standing, {
    teamId: 0,
    workshops: [{
      workshopId: 'north', phase: 'training', trainingTeamId: 0,
      progressTicks: 24, requiredTicks: 60,
    }],
  })!;
  assert.equal(ours.stalled, false);
  assert.match(ours.line, /Training at the north Workshop — 24 of 60 ticks\./);

  const theirs = trainingRead(standing, {
    teamId: 0,
    workshops: [{
      workshopId: 'north', phase: 'training', trainingTeamId: 1,
      progressTicks: 5, requiredTicks: 60,
    }],
  })!;
  assert.equal(theirs.stalled, true);
  assert.match(theirs.line, /their channel is running, not ours/);

  const reset = trainingRead(standing, {
    teamId: 0,
    interrupted: 'damaged',
    workshops: [{
      workshopId: 'north', phase: 'ready', trainingTeamId: null,
      progressTicks: 0, requiredTicks: 60,
    }],
  })!;
  assert.match(reset.line, /progress reset because it took damage/);
  // Every published interruption reason has player words.
  for (const reason of ['left-tile', 'moved', 'damaged', 'acted-in-combat',
    'acquired-core', 'died', 'contested'] as const) {
    assert.ok(trainingInterruptionPhrase(reason).length > 0);
  }
});

test('the editor previews a trainee on the catalog’s own training tile', () => {
  assert.deepEqual(workshopTrainingTile(catalog, 'north'), [18, 4]);
  assert.deepEqual(workshopTrainingTile(catalog, 'south'), [18, 22]);
  // 'either' has to pick one for a preview; the executor's own choice is a
  // match-time fact the editor cannot know.
  assert.deepEqual(workshopTrainingTile(catalog, 'either'), [18, 4]);
});

test('every starter recipe lands on legal Broadloom ground', () => {
  assert.equal(catalog.map.width, 37);
  assert.equal(catalog.map.height, 27);
  // The two mid pillars the recipes must not post on or face into.
  assert.equal(isLegalGround([15, 13]), false);
  assert.equal(isLegalGround([21, 13]), false);

  for (const recipe of RECIPES) {
    for (const group of recipe.groups) {
      const hint = group.hint;
      if (!hint) continue;
      const [x0, y0, x1, y1] = hint.rect;
      assert.ok(x0 <= x1 && y0 <= y1,
        `${recipe.id}/${group.name} hint rectangle is not normalized`);
      assert.ok(x1 < catalog.map.width && y1 < catalog.map.height,
        `${recipe.id}/${group.name} hint rectangle leaves the map`);
      // A hint is a suggestion BOX, so it only has to contain real standing
      // room — a rectangle whose every tile is wall is advice nobody can take.
      const standing: [number, number][] = [];
      for (let y = y0; y <= y1; y++) {
        for (let x = x0; x <= x1; x++) {
          if (isLegalGround([x, y])) standing.push([x, y]);
        }
      }
      assert.ok(standing.length >= 8,
        `${recipe.id}/${group.name} hint rectangle has ${standing.length} usable tiles`);
    }
    for (const phase of recipe.phases) {
      for (const job of phase.jobs) {
        if (!job?.objective) continue;
        assert.ok(isLegalGround(job.objective),
          `${recipe.id}/${phase.name} objective ${job.objective} is not open from both seats`);
      }
    }
  }
});

test('recipes with prefilled ground validate against the Broadloom catalog', () => {
  for (const recipe of RECIPES) {
    const document = makeBroadloomDocument(catalog);
    const { tactic } = instantiateRecipe(recipe, document, catalog);
    document.tactics.push(tactic);
    const errors = validateDocument(document, catalog)
      .filter((issue) => issue.severity === 'error')
      // Recipes deliberately leave the player's own drawing undone; only the
      // ground a recipe DOES prefill is this test's business.
      .filter((issue) => !/needs its (area|route|line|destination|approach point)/.test(issue.message))
      .filter((issue) => !/needs one post per bot/.test(issue.message))
      .filter((issue) => !/first phase needs at least one Group/.test(issue.message));
    assert.deepEqual(errors, [], `${recipe.id} does not fit Broadloom`);
    for (const phase of tactic.phases) {
      for (const job of Object.values(phase.jobs)) {
        for (const post of job.posts ?? []) {
          assert.ok(isLegalGround(post.at),
            `${recipe.id}/${phase.name} suggested a post on ${post.at}`);
        }
      }
    }
  }
});

test('the shipped Broadloom sheets the server compiles also open in the editor', () => {
  // The strongest mirror check there is: documents the C# compiler already
  // accepts — Workshop training included — must produce no editor error.
  for (const path of [
    '../../arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/first-stand-v2.json',
    '../../arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/tempered-broadloom-v1.json',
  ]) {
    const raw = JSON.parse(
      readFileSync(new URL(path, import.meta.url), 'utf8')) as TacticsDocument;
    assert.equal(raw.mapWidth, 37);
    assert.equal(raw.mapHeight, 27);
    const document = normalizeDocument(raw);
    const roster = withClasses(
      catalog, document.squad.map((bot) => bot.class));
    const errors = validateDocument(document, roster)
      .filter((issue) => issue.severity === 'error');
    assert.deepEqual(errors, [], `${path} does not open cleanly`);
  }
  const first = normalizeDocument(JSON.parse(readFileSync(
    new URL('../../arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/first-stand-v2.json',
      import.meta.url), 'utf8')));
  const trainee = first.tactics
    .flatMap((tactic) => tactic.phases)
    .flatMap((phase) => Object.values(phase.jobs))
    .find((job) => job.kind === 'train');
  assert.equal(trainee?.workshop, 'north');
  assert.equal(trainee?.posture, 'wait');
});
