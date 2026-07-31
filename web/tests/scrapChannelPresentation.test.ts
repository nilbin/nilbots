import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
// Through the SSR-built harness, like every other presenter test here: the
// presenter resolves looks and accents, and that reaches `import.meta.glob`,
// which only exists inside Vite.
import {
  createPresenter,
  frontlineCaptureVisual,
  loadReplayObject,
} from './.harness/harness.entry.js';
import type { ReplayV3Document } from '../src/replayWireV3.ts';
import { adaptReplayV3ToFrontline } from './support/replayFixtureInputs.ts';

/**
 * The two mechanics the viewer has to *draw*, pinned where they are derived.
 *
 * Both are read out of facts the wire already carries — a claim that moved,
 * this tick's damage, a tier vector that went up — so the whole reading lives
 * in `replayPresentation` and both renderers plus the panel consume it. These
 * tests are what stop the three of them from re-deciding it separately, and
 * the last one is what stops a replay written before either arm existed from
 * quietly acquiring the new picture.
 */

const CHANNEL_POLICY =
  'stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-opposition-erodes-at-multiple-then-builds';
const THRESHOLD = 8;

test('the capture channel resolves control by stillness and names the escort', () => {
  const presenter = createPresenter(bastion());
  const at = presenter.at(1);
  const objective = at.objective;
  assert.equal(objective?.kind, 'frontline');
  if (objective?.kind !== 'frontline') return;

  assert.equal(objective.channel, true);
  assert.equal(objective.channelGainCap, 2);
  // One stationary body of weight 1 on the point against no opposing denial.
  assert.equal(objective.channelGain, 1);
  assert.equal(objective.captureTeamId, 0);
  assert.equal(objective.captureContested, false);
  assert.equal(objective.channelingUnitCount, 1);
  assert.equal(objective.captureThreshold, THRESHOLD);

  assert.deepEqual(
    at.units.map((unit) => [unit.teamId, unit.channelRole]),
    [
      [0, 'channeling'],
      // The enemy is six tiles away on another position: not on the point, and
      // too far to be escorting anybody.
      [1, null],
    ],
  );
});

test('a hit on a body holding the point reads as an interrupt, not as a shorter bar', () => {
  const presenter = createPresenter(bastion());
  const objective = presenter.at(1).objective;
  assert.equal(objective?.kind, 'frontline');
  if (objective?.kind !== 'frontline') return;

  assert.deepEqual(objective.captureRevert, {
    kind: 'interrupt',
    amount: 3,
    fraction: 3 / THRESHOLD,
    fromFraction: 5 / THRESHOLD,
    teamId: 0,
    at: [{ x: 1, y: 3 }],
    ticksSince: 0,
    strength: 1,
  });
  assert.equal(objective.phase, 'participant-10 INTERRUPTED · −3');

  // And the renderers get it as a knockback: the length the meter had, at full
  // strength, outside the length it has.
  const visual = frontlineCaptureVisual(presenter.at(1));
  assert.ok(visual);
  assert.equal(visual.channel, true);
  assert.equal(visual.revert?.kind, 'interrupt');
  assert.equal(visual.revert?.strength, 1);
  assert.equal(visual.revert?.ghostFraction, 5 / THRESHOLD);
  assert.equal(visual.progressFraction, 2 / THRESHOLD);
  assert.equal(visual.revertAccent, visual.claimantAccent);
});

test('the same loss without damage on the point reads as a steady erosion', () => {
  // One change from the interrupt fixture: the bolt lands somewhere that is
  // not the objective region. The declared interrupt scope is bodies of the
  // controlling team standing *on* the point, and nothing else reverts
  // anything — so the identical drop is the enemy grinding the claim down.
  const presenter = createPresenter(
    bastion((document) => {
      const damage = document.ticks[1].events.find(
        (event) => event.kind === 'damage',
      )!;
      (damage.payload as { position: { x: number; y: number } }).position = {
        x: 4,
        y: 5,
      };
    }),
  );
  const objective = presenter.at(1).objective;
  assert.equal(objective?.kind, 'frontline');
  if (objective?.kind !== 'frontline') return;

  assert.equal(objective.captureRevert?.kind, 'erosion');
  assert.equal(objective.captureRevert?.amount, 3);
  assert.deepEqual(objective.captureRevert?.at, []);
  assert.equal(objective.phase, 'participant-10 CLAIM ERODING · −3');
  assert.equal(
    frontlineCaptureVisual(presenter.at(1))?.progressDirection,
    'eroding',
  );
});

test('the economy publishes both banks, live piles and the purchase beat', () => {
  const presenter = createPresenter(bastion());
  const economy = presenter.at(1).economy;
  assert.ok(economy);

  assert.deepEqual(
    economy.teams.map((team) => [team.teamId, team.bank, team.tierTotal]),
    [
      [0, 2, 1],
      [1, 3, 0],
    ],
  );
  assert.deepEqual(
    economy.teams[0].tracks.map((track) => [
      track.trackId,
      track.tier,
      track.maxTier,
      track.nextCost,
      track.affordable,
    ]),
    [
      ['edge', 1, 2, 10, false],
      ['plate', 0, 2, 10, false],
      ['optic', 0, 2, 10, false],
    ],
  );

  // The purchase telegraph: a tier that went up between two ticks is the buy,
  // and it stays a visible beat rather than a single 200ms frame.
  assert.deepEqual(economy.purchases, [
    {
      teamId: 0,
      teamName: 'participant-10',
      accent: economy.teams[0].accent,
      trackId: 'edge',
      label: 'edge',
      tier: 1,
      ticksSince: 0,
      strength: 1,
    },
  ]);
  assert.equal(economy.teams[0].tracks[0].boughtTicksSince, 0);

  assert.deepEqual(economy.piles, [
    {
      position: { x: 4, y: 1 },
      amount: 6,
      expiresAtTick: 81,
      remainingTicks: 80,
      lifeFraction: 1,
      expiring: false,
      vein: true,
    },
    {
      position: { x: 5, y: 5 },
      amount: 2,
      expiresAtTick: 11,
      remainingTicks: 10,
      lifeFraction: 0.125,
      expiring: true,
      vein: false,
    },
  ]);
  assert.equal(economy.carryCapacity, 6);
  assert.equal(economy.veinSites.length, 2);
  assert.equal(economy.nextVeinTick, 40);
  assert.equal(economy.veinDueNow, false);
});

test('a loaded body is a courier the whole viewer can see', () => {
  const at = createPresenter(bastion()).at(1);
  const carrier = at.units.find((unit) => unit.teamId === 1);
  assert.equal(carrier?.carriedScrap, 3);
  assert.equal(carrier?.carriedFraction, 0.5);
  assert.equal(
    at.units.find((unit) => unit.teamId === 0)?.carriedScrap,
    0,
  );
  assert.equal(at.economy?.teams[1].carried, 3);
});

test('a replay whose ruleset declares neither arm carries neither picture', () => {
  const plain = createPresenter(
    loadReplayObject(adaptReplayV3ToFrontline(source())).replay,
  ).at(1);
  const objective = plain.objective;
  assert.equal(objective?.kind, 'frontline');
  if (objective?.kind !== 'frontline') return;

  assert.equal(plain.economy, null);
  assert.equal(objective.channel, false);
  assert.equal(objective.channelGain, null);
  assert.equal(objective.channelGainCap, null);
  assert.equal(objective.captureRevert, null);
  assert.equal(objective.channelingUnitCount, 0);
  assert.equal(objective.screeningUnitCount, 0);
  assert.deepEqual(
    plain.units.map((unit) => [unit.carriedScrap, unit.channelRole]),
    plain.units.map(() => [0, null]),
  );
  const visual = frontlineCaptureVisual(plain);
  assert.equal(visual?.channel, false);
  assert.equal(visual?.revert, null);
});

function source(): ReplayV3Document {
  return JSON.parse(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ) as ReplayV3Document;
}

/**
 * The shipped composite: the capture channel plus the scrap economy, on the
 * generic fixture's two stationary bodies.
 *
 * Team 0 stands on the active objective for both ticks — stationary by the
 * channel's own definition, since its tile did not change — with a claim of 5
 * that a bolt takes 3 back off. Team 1 stands six tiles away carrying a load,
 * and team 0 spends its bank on a tier between the two ticks.
 */
function bastion(
  mutate?: (document: ReplayV3Document) => void,
): ReturnType<typeof loadReplayObject>['replay'] {
  const document = adaptReplayV3ToFrontline(source());
  const mode = document.header.contract.rules.gameMode;
  if (mode.kind !== 'frontline') throw new Error('expected a Frontline mode');

  // The stock fixture's one form has no objective weight at all, which under
  // the channel means it can neither claim nor deny — and neither channel nor
  // carry. A body that counts is the whole point here.
  for (const form of document.header.contract.rules.forms)
    form.objectiveWeight = 1;

  mode.capture = {
    ...mode.capture,
    threshold: THRESHOLD,
    controlPolicy: CHANNEL_POLICY,
    decayClock:
      'empty-and-contested-ticks-preserve-claim-enemy-sole-erosion-only',
    stationaryGainMultiplierCap: 2,
    opposingErosionMultiplier: 4,
    claimInterrupt: {
      kind: 'damage-to-controller-on-objective-reverts-work',
      revertPerDamagePoint: 1,
      scope: 'controlling-team-bodies-on-active-objective-region',
      granularity: 'whole-run',
    },
  };
  mode.scrapEconomy = {
    veinSites: [
      { x: 4, y: 1 },
      { x: 4, y: 5 },
    ],
    veinFirstSpawnTick: 40,
    veinSpawnIntervalTicks: 40,
    veinLastSpawnTick: 120,
    veinAmount: 6,
    wreckAmount: 1,
    assayAmount: 1,
    carryCapacity: 6,
    pileLifetimeTicks: 80,
    maxSimultaneousPiles: 16,
    bankRegionIds: ['team-0-home-pad', 'team-1-home-pad'],
    upgradeScope: 'prime-slot-lives-only',
    maxTotalTiers: 3,
    purchaseMode: 'invest-action',
    tracks: [
      {
        trackId: 'edge',
        effect: 'mobile-attack-travel-tiles-delta',
        perTierMagnitude: 1,
        maxTier: 2,
        tierCosts: [10, 10],
      },
      {
        trackId: 'plate',
        effect: 'spawn-max-health-delta',
        perTierMagnitude: 1,
        maxTier: 2,
        tierCosts: [10, 10],
      },
      {
        trackId: 'optic',
        effect: 'vision-range-delta',
        perTierMagnitude: 1,
        maxTier: 2,
        tierCosts: [10, 10],
      },
    ],
  };

  // Put the active objective under the body that never moves, so the fixture
  // has a genuine channeler rather than a claim nobody is standing on.
  document.header.contract.map.regions = [
    { regionId: 'frontline-low', kind: 'objective', tiles: [[1, 3]] },
    { regionId: 'frontline-centre', kind: 'objective', tiles: [[4, 3]] },
    { regionId: 'frontline-high', kind: 'objective', tiles: [[7, 3]] },
  ];

  const control = (
    captureProgress: number,
    scrapTeams: { teamId: number; bank: number; tierLevels: number[] }[],
    scrapPiles: {
      position: { x: number; y: number };
      amount: number;
      expiresAtTick: number;
    }[],
  ) => ({
    kind: 'frontline' as const,
    modeId: 'frontline',
    activePositionIndex: 0,
    claimingTeamId: 0,
    captureProgress,
    decayTicksElapsed: 0,
    controlResumesAtTick: 0,
    holdOwnerTeamId: null,
    holdEndsAtTick: null,
    secondaryOwnerTeamId: null,
    secondaryClaimProgress: 0,
    scrapTeams,
    scrapPiles,
  });

  const opening = control(
    5,
    [
      { teamId: 0, bank: 12, tierLevels: [0, 0, 0] },
      { teamId: 1, bank: 3, tierLevels: [0, 0, 0] },
    ],
    [{ position: { x: 4, y: 1 }, amount: 6, expiresAtTick: 81 }],
  );
  // Tick 1: three points of work taken back, and the bank turned into a tier.
  const closing = control(
    2,
    [
      { teamId: 0, bank: 2, tierLevels: [1, 0, 0] },
      { teamId: 1, bank: 3, tierLevels: [0, 0, 0] },
    ],
    [
      { position: { x: 4, y: 1 }, amount: 6, expiresAtTick: 81 },
      { position: { x: 5, y: 5 }, amount: 2, expiresAtTick: 11 },
    ],
  );

  // The scoreboard is not free to disagree with the control state: the
  // validator recomputes `advance × (index − centre) × threshold + signed
  // claim` and rejects a document whose territorial progress does not match.
  const centre = Math.floor(mode.frontlinePositionCount / 2);
  const applyControl = (
    state: { mode: unknown; scoreboard: { teams: { teamId: number; scores: { channel: string; value: string }[] }[] } },
    control: ReturnType<typeof control>,
  ) => {
    state.mode = { ...control };
    for (const team of state.scoreboard.teams) {
      const delta = team.teamId === 0 ? 1 : -1;
      const claim =
        control.claimingTeamId === null
          ? 0
          : control.claimingTeamId === team.teamId
            ? control.captureProgress
            : -control.captureProgress;
      team.scores = [
        {
          channel: 'territorial-progress',
          value: String(
            delta * (control.activePositionIndex - centre) * THRESHOLD + claim,
          ),
        },
      ];
    }
  };

  applyControl(document.initialFrame.state, opening);
  applyControl(document.ticks[0].tickStart.state, opening);
  applyControl(document.ticks[0].postState, opening);
  applyControl(document.ticks[1].tickStart.state, opening);
  applyControl(document.ticks[1].postState, closing);
  for (const tick of document.ticks)
    for (const turn of tick.actorTurns) {
      turn.observation.mode = { ...tick.tickStart.state.mode };
      turn.observation.scoreboard = structuredClone(
        tick.tickStart.state.scoreboard,
      );
    }

  // The result restates the final tick's control and scores, and the
  // validator checks that it does.
  const finalWorld = document.ticks[1].postState;
  if (document.result === null)
    throw new Error('the fixture is a complete replay');
  document.result.mode = {
    kind: 'frontline',
    reason: 'max-ticks',
    control: { ...closing },
    scores: finalWorld.scoreboard.teams.map((team) => ({
      teamId: team.teamId,
      territorialProgress: team.scores[0]!.value,
    })),
  };
  // Standings are ranked from those same scores, highest first, and the
  // validator recomputes that too. Team 1 is a position ahead here, so it
  // takes the timeout.
  document.result.standings.teams = [...document.result.standings.teams]
    .map((standing) => ({
      ...standing,
      scores: structuredClone(
        finalWorld.scoreboard.teams.find(
          (team) => team.teamId === standing.teamId,
        )!.scores,
      ),
    }))
    .sort(
      (left, right) =>
        Number(right.scores[0]!.value) - Number(left.scores[0]!.value),
    )
    .map((standing, index) => ({
      ...standing,
      rank: index + 1,
      outcome: index === 0 ? ('win' as const) : ('loss' as const),
    }));
  document.result.standings.winnerTeamId =
    document.result.standings.teams[0]!.teamId;

  // The interrupt: the bolt that lands on the body holding the point.
  const damage = document.ticks[1].events.find(
    (event) => event.kind === 'damage',
  )!;
  const payload = damage.payload as {
    amount: number;
    position: { x: number; y: number };
    targetActorId: { teamId: number };
  };
  payload.amount = 3;
  payload.position = { x: 1, y: 3 };
  assert.equal(payload.targetActorId.teamId, 0);

  // The courier: team 1's body is carrying half a load home.
  for (const turn of document.ticks[1].actorTurns) {
    if (turn.observation.self.actorId.teamId === 1)
      turn.observation.self.carriedScrap = 3;
    for (const enemy of turn.observation.enemies)
      if (enemy.actorId.teamId === 1) enemy.carriedScrap = 3;
  }

  mutate?.(document);
  return loadReplayObject(document).replay;
}
