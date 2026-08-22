import type { TacticalSheetCatalog } from '../../src/site/api';
import { blankDocument, type TacticsDocument } from '../../src/site/tactics/tacticsDocument';

/**
 * The shipped Broadloom map (`arc-relay-broadloom-01`, 37x27) as the sheet
 * catalog delivers it. Terrain is byte-identical to
 * `ArcRelayH0Definition.CreateBroadloomMap`, and the landmarks, spawn anchors
 * and protected pads are that builder's own values — the editor's geometry
 * rules only mean something against the real rooms, mouths and pillars.
 *
 * The lower half is the 180-degree rotation of the upper half, which is what
 * makes `tilePlacementIssue`'s both-seats check load-bearing here.
 */

export const BROADLOOM_ROWS = [
  '#####################################',
  '#..........#.............#..........#',
  '#......##.....####.####......##...#.#',
  '#....#...........#....#.........#...#',
  '#..#########........#....###....#...#',
  '#..###.....#..#.#......#.....#..#...#',
  '#....#.....##.#..........#...##.#...#',
  '#.#...........##.....##......#......#',
  '#.....##.##......###.......#........#',
  '###..........##..###...##......######',
  '#.....###.....#........#..###.......#',
  '#.........#...#.#...#.......#.......#',
  '#.........#............#..#.........#',
  '#.........##...#.....#...##.........#',
  '#.........#..#............#.........#',
  '#.......#.......#...#.#...#.........#',
  '#.......###..#........#.....###.....#',
  '######......##...###..##..........###',
  '#........#.......###......##.##.....#',
  '#......#......##.....##...........#.#',
  '#...#.##...#..........#.##.....#....#',
  '#...#..#.....#......#.#..#.....###..#',
  '#...#....###....#........#########..#',
  '#...#.........#....#...........#....#',
  '#.#...##......####.####.....##......#',
  '#..........#.............#..........#',
  '#####################################',
];

export const BROADLOOM_WIDTH = 37;
export const BROADLOOM_HEIGHT = 27;

/** The map's own 180-degree rotation — the seat mirror the compiler applies. */
export function rotate([x, y]: [number, number]): [number, number] {
  return [BROADLOOM_WIDTH - 1 - x, BROADLOOM_HEIGHT - 1 - y];
}

export function isOpen([x, y]: [number, number]): boolean {
  return x >= 0 && y >= 0 && x < BROADLOOM_WIDTH && y < BROADLOOM_HEIGHT
    && BROADLOOM_ROWS[y][x] !== '#';
}

/** Open from BOTH seats, which is the only kind of tile a sheet may name. */
export function isLegalGround(point: [number, number]): boolean {
  return isOpen(point) && isOpen(rotate(point));
}

const point = ([x, y]: [number, number]) => ({ x, y });

function rectangle(
  x0: number, y0: number, x1: number, y1: number,
): { x: number; y: number }[] {
  const tiles: { x: number; y: number }[] = [];
  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) tiles.push({ x, y });
  }
  return tiles;
}

const WORKSHOP_NORTH_CONTEST: [number, number][] = [
  [18, 2], [14, 3], [15, 3], [16, 3], [18, 3], [19, 3], [20, 3], [21, 3],
  [14, 4], [15, 4], [16, 4], [17, 4], [18, 4], [19, 4], [21, 4], [22, 4],
  [15, 5], [17, 5], [18, 5], [19, 5], [20, 5], [21, 5], [22, 5],
  [15, 6], [16, 6], [17, 6], [18, 6], [19, 6], [20, 6], [21, 6], [22, 6],
  [16, 7], [17, 7], [18, 7], [19, 7], [20, 7],
];

const SPAWN_WEST: [number, number][] = [
  [1, 11], [2, 11], [3, 11], [4, 11], [1, 15], [2, 15], [3, 15], [4, 15],
];

function signatureAuthoring(flags: Partial<{
  exactAnchor: boolean; routeForward: boolean; friendlyRole: boolean;
}> = {}) {
  return {
    category: 'strike', argumentKind: 'target', engagementRange: 6,
    cooldownTicks: 40, activationTiming: 'immediate', initialPhase: 'ready',
    maturityPhase: 'ready', cooldownAnchor: 'use', parameters: {},
    allowedSelectors: ['nearest-enemy'],
    allowsFriendlyRole: flags.friendlyRole ?? false,
    allowsMostExperiencedAlly: false,
    allowsExactAnchor: flags.exactAnchor ?? false,
    allowsRouteAheadRole: false,
    allowsRouteForward: flags.routeForward ?? false,
    guidanceGeometry: flags.routeForward
      ? 'line' : flags.exactAnchor ? 'points' : 'none',
    guidanceMarkLimit: flags.routeForward ? 8 : flags.exactAnchor ? 2 : 0,
    durableDeployment: false,
    defaultCommitmentBehavior: 'continueNormally',
    preset: {
      id: 'p', title: 'p', description: 'p', targetKind: 'namedSelector',
      namedTarget: 'nearest-enemy', suggestsActivationZone: true,
    },
  };
}

export function makeBroadloomCatalog(): TacticalSheetCatalog {
  const classRow = (
    id: string,
    name: string,
    flags?: Parameters<typeof signatureAuthoring>[0],
  ) => ({
    id, name, signatureId: id, actionId: id, signatureName: `${name} Signature`,
    fantasy: '', starter: true, unlocked: true,
    signatureAuthoring: signatureAuthoring(flags),
  });
  return {
    playlistKey: 'arc-relay-entrants',
    playlistVersionId: '00000000-0000-4000-8000-000000000033',
    map: {
      id: 'arc-relay-broadloom-01',
      version: 1,
      formatVersion: 1,
      width: BROADLOOM_WIDTH,
      height: BROADLOOM_HEIGHT,
      tileRows: BROADLOOM_ROWS,
      regions: [
        { id: 'heal-north', kind: 'Objective', tiles: [point([8, 6])] },
        { id: 'heal-south', kind: 'Objective', tiles: [point([28, 20])] },
        { id: 'well-north', kind: 'Objective', tiles: [point([26, 7])] },
        { id: 'well-centre', kind: 'Objective', tiles: [point([18, 13])] },
        { id: 'well-south', kind: 'Objective', tiles: [point([10, 19])] },
        { id: 'reactor-west', kind: 'Objective', tiles: [point([3, 13])] },
        { id: 'reactor-east', kind: 'Objective', tiles: [point([33, 13])] },
        { id: 'home-west', kind: 'Objective', tiles: rectangle(5, 7, 11, 15) },
        {
          id: 'home-east',
          kind: 'Objective',
          tiles: rectangle(5, 7, 11, 15).map((tile) =>
            point(rotate([tile.x, tile.y]))),
        },
        {
          id: 'veterancy-workshop-north-train',
          kind: 'Objective',
          tiles: [point([18, 4])],
        },
        {
          id: 'veterancy-workshop-south-train',
          kind: 'Objective',
          tiles: [point([18, 22])],
        },
        {
          id: 'veterancy-workshop-north-contest',
          kind: 'Objective',
          tiles: WORKSHOP_NORTH_CONTEST.map(point),
        },
        {
          id: 'veterancy-workshop-south-contest',
          kind: 'Objective',
          tiles: WORKSHOP_NORTH_CONTEST.map((tile) => point(rotate(tile))),
        },
      ],
      spawnAnchors: [
        ...SPAWN_WEST.map((at, unitId) => ({
          id: `team-0-${unitId}`, position: point(at), facing: 'East',
        })),
        ...SPAWN_WEST.map((at, unitId) => ({
          id: `team-1-${unitId}`, position: point(rotate(at)), facing: 'West',
        })),
      ],
      tileTags: [
        {
          id: 'home-pads',
          kind: 'SpawnProtected',
          tiles: [
            ...rectangle(1, 4, 10, 16),
            ...rectangle(1, 4, 10, 16).map((tile) =>
              point(rotate([tile.x, tile.y]))),
          ],
        },
        {
          id: 'arc-required-tiles',
          kind: 'SignaturePlacementForbidden',
          tiles: ([
            [26, 7], [18, 13], [10, 19], [3, 13], [33, 13], [18, 4], [18, 22],
          ] as [number, number][]).map(point),
        },
      ],
    },
    slotCount: 8,
    maximumCopiesPerClass: 2,
    minimumDistinctClasses: 7,
    classes: [
      classRow('kestrel', 'Kestrel', { routeForward: true }),
      classRow('switchback', 'Switchback', { friendlyRole: true }),
      classRow('longshot', 'Longshot'),
      classRow('mason', 'Mason', { exactAnchor: true }),
      classRow('lantern', 'Lantern', { exactAnchor: true }),
      classRow('palisade', 'Palisade'),
      classRow('relay', 'Relay', { friendlyRole: true }),
      classRow('patchbay', 'Patchbay', { friendlyRole: true }),
    ],
    starterSheets: [],
    stockOpponents: [],
  } as unknown as TacticalSheetCatalog;
}

/** The same catalog widened to whatever roster a real saved sheet fields.
 * A class the fixture does not model gets the plain no-marks descriptor, so
 * validation still judges geometry, groups and rows rather than the roster. */
export function withClasses(
  catalog: TacticalSheetCatalog,
  classIds: readonly string[],
): TacticalSheetCatalog {
  const extra = classIds
    .filter((id) => !catalog.classes.some((entry) => entry.id === id))
    .map((id) => ({
      id, name: id, signatureId: id, actionId: id, signatureName: id,
      fantasy: '', starter: true, unlocked: true,
      signatureAuthoring: signatureAuthoring(),
    }));
  return {
    ...catalog,
    classes: [...catalog.classes, ...extra],
  } as unknown as TacticalSheetCatalog;
}

/** Eight bots, one per class the catalog ships — the roster shape every
 * validation path expects before it will look at a Tactic at all. */
export function makeBroadloomDocument(
  catalog: TacticalSheetCatalog,
): TacticsDocument {
  const document = blankDocument(catalog);
  document.squad = catalog.classes.map((entry, slot) => ({
    slot, class: entry.id, build: 'balanced' as const, name: `Bot${slot}`,
  }));
  return document;
}
