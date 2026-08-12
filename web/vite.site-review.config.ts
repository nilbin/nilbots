import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig, type Connect, type Plugin } from 'vite';
import {
  REVIEW_LABS_PLAYLIST_ID,
  REVIEW_LAUNCHED_MATCH_ID,
  REVIEW_LIVE_MATCH_ID,
  REVIEW_PINCER_ID,
  REVIEW_SET_ID,
  REVIEW_WARDEN_ID,
  reviewSetGameSpecs,
  type ReviewSetGameSpec,
} from './src/site/review/fixtures';
import {
  siteReviewApiResponse,
  siteReviewRequestKey,
} from './src/site/review/routes';

const SITE_REVIEW_HEADER = 'x-nilbots-site-review';
const REVIEW_SHEET_ID = '74000000-0000-4000-8000-000000000001';
const sourceReplay = readFileSync(
  new URL('./tests/fixtures/golden-replay.json', import.meta.url),
  'utf8',
).trim();
const taplineTemplatePlaybook = readFileSync(
  new URL(
    '../arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/starter-tapline-v2.json',
    import.meta.url,
  ),
  'utf8',
).trim();
const taplineTemplateLayout = readFileSync(
  new URL(
    '../arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/map-plans/starter-tapline-v2.json',
    import.meta.url,
  ),
  'utf8',
).trim();
const taplineReviewCatalog = makeTaplineReviewCatalog(
  taplineTemplatePlaybook,
  taplineTemplateLayout,
);
const completedReviewReplays = new Map<string, string>(
  reviewSetGameSpecs.map((spec) => [
    `/api/matches/${spec.id}/replay`,
    makeCompletedReviewReplay(sourceReplay, spec),
  ]),
);
const liveReviewReplay = makeLiveReviewReplay(sourceReplay, [
  {
    name: 'Warden gen-1',
    artifactHash: '4f9229f8eb7b7725',
    accent: '#7dd3fc',
    lookId: 'aureate-warden',
  },
  {
    name: 'Rampart gen-2',
    artifactHash: '77dba5d2fe1939ac',
    accent: '#bef264',
    lookId: 'orbiter',
  },
]);
const launchedMatchReplay = makeLiveReviewReplay(sourceReplay, [
  {
    name: 'Pincer gen-10',
    artifactHash: '9f31c0a4b7de51aa',
    accent: '#22d3ee',
    lookId: 'vanguard',
  },
  {
    name: 'Warden gen-1',
    artifactHash: '4f9229f8eb7b7725',
    accent: '#7dd3fc',
    lookId: 'aureate-warden',
  },
]);

/**
 * The review API exists only in this config. Neither the normal Vite config nor the
 * browser entry point imports it, so a production build cannot silently acquire fixtures.
 */
function siteReviewApi(): Plugin {
  let savedSheet: ReviewSheet | null = null;
  const middleware: Connect.NextHandleFunction = (request, response, next) => {
    const requestUrl = request.url ?? '/';
    const url = new URL(requestUrl, 'http://nilbots.site-review');

    if (url.pathname === '/__site-review/health') {
      if (
        request.method?.toUpperCase() === 'GET' &&
        url.search === ''
      ) {
        sendJson(response, 200, { ready: true });
        return;
      }
      response.setHeader(
        'x-nilbots-site-review-error',
        'invalid-health-request',
      );
      sendJson(response, 500, {
        ready: false,
        detail: 'Health accepts exactly GET /__site-review/health.',
      });
      return;
    }
    if (!url.pathname.startsWith('/api/')) {
      next();
      return;
    }

    const sheetMutation = sheetReviewMutation(request.method, url);
    if (sheetMutation !== null) {
      void handleSheetReviewMutation(
        request,
        response,
        sheetMutation,
        savedSheet,
        (sheet) => { savedSheet = sheet; },
      );
      return;
    }

    const sheetReview = sheetReviewResponse(request.method, url, savedSheet);
    if (sheetReview !== undefined) {
      sendJson(response, 200, sheetReview);
      return;
    }

    if (
      request.method?.toUpperCase() === 'GET' &&
      url.pathname === '/api/accounts/external/google' &&
      url.search === ''
    ) {
      response.statusCode = 302;
      response.setHeader('location', '/login?review=google');
      response.setHeader('cache-control', 'no-store');
      response.setHeader(SITE_REVIEW_HEADER, '1');
      response.end();
      return;
    }

    const launchKind = reviewArenaLaunchKind(request.method, url);
    if (launchKind !== null) {
      void handleReviewArenaLaunch(request, response, launchKind);
      return;
    }

    if (request.method?.toUpperCase() !== 'GET') {
      response.setHeader('x-nilbots-site-review-expected-status', '409');
      response.setHeader('x-nilbots-site-review-error', 'read-only-api');
      sendJson(response, 409, {
        type: 'about:blank',
        title: 'Site review is read-only',
        status: 409,
        detail:
          'This public design-review build does not submit or change account data.',
      });
      return;
    }

    const replay = reviewReplay(request.method, url);
    if (replay !== null) {
      sendRawJson(response, 200, replay);
      return;
    }

    const key = siteReviewRequestKey(request.method, requestUrl);
    const fixture = siteReviewApiResponse(request.method, requestUrl, {
      referer: request.headers.referer,
    });
    if (fixture === undefined) {
      const detail = `Unmatched site-review API request: ${key}`;
      console.error(detail);
      response.setHeader('x-nilbots-site-review-error', 'unmatched-api-request');
      sendJson(response, 500, {
        type: 'about:blank',
        title: 'Unmatched site-review API request',
        status: 500,
        detail,
      });
      return;
    }

    if (fixture.status >= 400) {
      response.setHeader(
        'x-nilbots-site-review-expected-status',
        String(fixture.status),
      );
    }
    sendJson(response, fixture.status, fixture.body);
  };

  return {
    name: 'nilbots-site-review-api',
    configureServer(server) {
      server.middlewares.use(middleware);
    },
    configurePreviewServer(server) {
      server.middlewares.use(middleware);
    },
  };
}

interface ReviewSheet {
  id: string;
  name: string;
  revision: number;
  contentHash: string;
  createdAt: string;
  updatedAt: string;
  ordersJson: string;
  mapPlanJson: string;
  entrant: Record<string, unknown>;
}

type SheetReviewMutation = 'create' | 'update' | 'trial';

function sheetReviewMutation(
  method: string | undefined,
  url: URL,
): SheetReviewMutation | null {
  if (url.search !== '') return null;
  const verb = method?.toUpperCase();
  if (verb === 'POST' && url.pathname === '/api/sheets') return 'create';
  if (verb === 'PUT' && url.pathname === `/api/sheets/${REVIEW_SHEET_ID}`) {
    return 'update';
  }
  if (
    verb === 'POST' &&
    url.pathname === `/api/sheets/${REVIEW_SHEET_ID}/trial`
  ) {
    return 'trial';
  }
  return null;
}

async function handleSheetReviewMutation(
  request: Parameters<Connect.NextHandleFunction>[0],
  response: Parameters<Connect.NextHandleFunction>[1],
  mutation: SheetReviewMutation,
  savedSheet: ReviewSheet | null,
  save: (sheet: ReviewSheet) => void,
) {
  try {
    const body = await readJsonRequest(request);
    if (mutation === 'trial') {
      if (savedSheet === null || !isRecord(body)) {
        throw new Error('A saved review sheet and trial body are required.');
      }
      response.setHeader('x-nilbots-site-review-simulated', 'sheet-trial');
      sendJson(response, 200, { id: REVIEW_LAUNCHED_MATCH_ID });
      return;
    }
    if (!isRecord(body)) throw new Error('The sheet body must be an object.');
    const name = body.name;
    const ordersJson = body.ordersJson;
    const mapPlanJson = body.mapPlanJson;
    if (
      typeof name !== 'string' ||
      typeof ordersJson !== 'string' ||
      typeof mapPlanJson !== 'string'
    ) {
      throw new Error('Name, ordersJson and mapPlanJson are required.');
    }
    const now = new Date().toISOString();
    const revision = mutation === 'update' ? (savedSheet?.revision ?? 0) + 1 : 1;
    const contentHash = createHash('sha256')
      .update(ordersJson)
      .update('\0')
      .update(mapPlanJson)
      .digest('hex');
    const next: ReviewSheet = {
      id: REVIEW_SHEET_ID,
      name,
      revision,
      contentHash,
      createdAt: savedSheet?.createdAt ?? now,
      updatedAt: now,
      ordersJson,
      mapPlanJson,
      entrant: reviewSheetEntrant(
        name,
        revision,
        contentHash,
        ordersJson,
        body.enterLadder !== false,
      ),
    };
    save(next);
    response.setHeader('x-nilbots-site-review-simulated', 'sheet-save');
    sendJson(response, 200, next);
  } catch (error) {
    response.setHeader('x-nilbots-site-review-error', 'invalid-sheet-review-body');
    sendJson(response, 400, {
      type: 'about:blank',
      title: 'Invalid review sheet request',
      status: 400,
      detail: error instanceof Error ? error.message : 'Invalid request.',
    });
  }
}

function reviewSheetEntrant(
  name: string,
  revision: number,
  contentHash: string,
  ordersJson: string,
  ladderOptedIn: boolean,
): Record<string, unknown> {
  const source = JSON.parse(ordersJson) as {
    roles?: Array<{ classes?: string[] }>;
  };
  const classIds = source.roles?.flatMap((role) => role.classes ?? []) ?? [];
  const classNames = new Map(
    taplineReviewCatalog.classes.map((entry) => [entry.id, entry.name]),
  );
  return {
    id: REVIEW_SHEET_ID,
    kind: 'sheet',
    name,
    ownerDisplayName: 'you',
    revision,
    crest: {
      key: 'review-orders-v2',
      variant: 0,
      shape: 'shield',
      pattern: 'split',
      mark: 'relay',
      primary: '#d47a39',
      secondary: '#241914',
      detail: '#efc08f',
    },
    composition: classIds.map((classId, slot) => ({
      slot,
      classId,
      className: classNames.get(classId) ?? classId,
      lookId: `arc-${classId}`,
    })),
    rating: 1000,
    rankedMatches: 0,
    ladderOptedIn,
    status: 'ready',
    suspensionReason: null,
    suspensionMatchId: null,
    artifactHash: null,
    contentHash,
    isOwner: true,
  };
}

function sheetReviewResponse(
  method: string | undefined,
  url: URL,
  savedSheet: ReviewSheet | null,
): unknown {
  if (method?.toUpperCase() !== 'GET' || url.search !== '') return undefined;
  if (url.pathname === '/api/sheets/catalog') return taplineReviewCatalog;
  if (url.pathname === '/api/sheets') {
    return savedSheet === null ? [] : [{
      id: savedSheet.id,
      name: savedSheet.name,
      revision: savedSheet.revision,
      contentHash: savedSheet.contentHash,
      createdAt: savedSheet.createdAt,
      updatedAt: savedSheet.updatedAt,
      entrant: savedSheet.entrant,
    }];
  }
  if (url.pathname === `/api/sheets/${REVIEW_SHEET_ID}`) return savedSheet;
  if (url.pathname === '/api/arc-relay/entrants') return [];
  if (url.pathname === '/api/arc-relay/seasons/current') {
    return {
      seasonId: '71000000-0000-4000-8000-000000000001',
      key: 'season-1',
      number: 1,
      name: 'Season 1',
      status: 'open',
      startsAt: '2026-07-28T00:00:00Z',
      endsAt: '2026-09-01T00:00:00Z',
      openedAt: '2026-07-28T00:00:00Z',
      closedAt: null,
      ladderId: '72000000-0000-4000-8000-000000000001',
      ladderName: 'Tapline Season 1',
      maximumFieldedPerAccount: 3,
      baselineMatchesPerEntrantPerDay: 6,
      standings: [],
      recentMatches: [],
    };
  }
  return undefined;
}

function makeTaplineReviewCatalog(
  templateOrdersJson: string,
  templateMapPlanJson: string,
) {
  const classes = [
    ['kestrel', 'Kestrel', 'Vector Dash', 'Rapid interceptor built to abandon one lane and answer another.'],
    ['palisade', 'Palisade', 'Prism Wall', 'Convoy shield whose projector face blanks a firing lane.'],
    ['towline', 'Towline', 'Tractor Hook', 'Formation disruptor with a visible winch, cable and hook.'],
    ['patchbay', 'Patchbay', 'Repair Beam', 'Field medic that trades its own gun tempo to preserve an ally.'],
    ['lantern', 'Lantern', 'Survey Flare', 'Mobile sensor mast that buys certainty before a commitment.'],
    ['mortar', 'Mortar', 'Falling Star', 'Lobbing artillery that punishes formations which stay put.'],
    ['minesmith', 'Minesmith', 'Trip Node', 'Route controller carrying visible nodes for watched ground.'],
    ['hush', 'Hush', 'Null Field', 'Dampener array that suppresses signatures instead of racing damage.'],
    ['relay', 'Relay', 'Arc Toss', 'Core runner with a cradle and arms for one risky long handoff.'],
    ['switchback', 'Switchback', 'Exchange', 'Paired mirrored frame that swaps which ally occupies danger.'],
    ['longshot', 'Longshot', 'Rail Line', 'A body-length rail that turns a public corridor into a threat.'],
    ['mason', 'Mason', 'Hardlight Block', 'Builder rig that bends a route with temporary cover.'],
    ['sunder', 'Sunder', 'Target Paint', 'Designator optics that tell a team which target matters now.'],
    ['repulsor', 'Repulsor', 'Kinetic Burst', 'Radial emitters that break a local formation from adjacency.'],
    ['veil', 'Veil', 'Smoke Canister', 'Smoke-launcher skirmisher that authors uncertainty on the map.'],
    ['nest', 'Nest', 'Sentinel Seed', 'Pod carrier that leaves one persistent, killable guard behind.'],
  ].map(([id, name, signatureName, fantasy], index) => ({
    id,
    name,
    signatureName,
    fantasy,
    starter: index < 5 || ['hush', 'relay', 'switchback'].includes(id),
    // The review account owns the launch band so every composition interaction
    // can be assessed without mixing entitlement states into the UX pass.
    unlocked: true,
  }));
  return {
    playlistKey: 'arc-relay-entrants',
    playlistVersionId: '73000000-0000-4000-8000-000000000020',
    map: makeTaplineReviewMap(),
    slotCount: 8,
    maximumCopiesPerClass: 2,
    classes,
    templateOrdersJson,
    templateMapPlanJson,
    stockOpponents: [{
      id: 'wellwright-v2',
      name: 'Wellwright v2',
      description: 'The Tapline stock opponent works all three Wells and explicitly returns every carried Core.',
      composition: ['kestrel', 'patchbay', 'relay', 'longshot', 'longshot', 'towline', 'towline', 'mason'],
    }],
  };
}

function makeTaplineReviewMap() {
  const tileRows = [
    '###############################',
    '#.............................#',
    '###.###########.###########.###',
    '#......#......................#',
    '#....#...#..###.###.##..###...#',
    '#....#...#...#...#..#....#....#',
    '#........#...#...#..#.##.#....#',
    '#........#.......#.......#....#',
    '#....#...###....##....#.......#',
    '###..........##.#..........##.#',
    '#.....###.##......###..##.....#',
    '#.##.......#.......#....#.....#',
    '#.....###..#.......#....#.....#',
    '#..##...##...........##...##..#',
    '#.....#....#.......#..###.....#',
    '#.....#....#.......#.......##.#',
    '#.....##..###......##.###.....#',
    '#.##..........#.##..........###',
    '#.......#....##....###...#....#',
    '#....#.......#.......#........#',
    '#....#.##.#..#...#...#........#',
    '#....#....#..#...#...#...#....#',
    '#...###..##.###.###..#...#....#',
    '#......................#......#',
    '###.###########.###########.###',
    '#.............................#',
    '###############################',
  ];
  const point = (x: number, y: number) => ({ x, y });
  const rotate = ({ x, y }: { x: number; y: number }) => point(30 - x, 26 - y);
  const rectangle = (minX: number, maxX: number, minY: number, maxY: number) =>
    Array.from({ length: maxY - minY + 1 }, (_, dy) =>
      Array.from({ length: maxX - minX + 1 }, (_, dx) => point(minX + dx, minY + dy)),
    ).flat();
  const westPad = rectangle(1, 4, 3, 8);
  const eastPad = westPad.map(rotate).sort((a, b) => a.y - b.y || a.x - b.x);
  const westHome = rectangle(6, 8, 6, 8);
  const eastHome = westHome.map(rotate).sort((a, b) => a.y - b.y || a.x - b.x);
  const westSpawns = [
    point(1, 4), point(2, 4), point(3, 4), point(4, 4),
    point(1, 5), point(3, 5), point(4, 5), point(1, 6),
  ];
  const objective = (id: string, tiles: { x: number; y: number }[]) => ({
    id,
    kind: 'Objective',
    tiles,
  });
  return {
    id: 'arc-relay-tapline-01',
    version: 1,
    formatVersion: 1,
    width: 31,
    height: 27,
    tileRows,
    regions: [
      objective('heal-north', [point(21, 7)]),
      objective('heal-south', [point(9, 19)]),
      objective('well-north', [point(11, 6)]),
      objective('well-centre', [point(15, 13)]),
      objective('well-south', [point(19, 20)]),
      objective('reactor-west', [point(2, 5)]),
      objective('reactor-east', [point(28, 21)]),
      objective('home-west', westHome),
      objective('home-east', eastHome),
    ],
    spawnAnchors: westSpawns.flatMap((position, unitId) => [
      { id: `team-0-unit-${unitId}`, position, facing: 'East' },
      { id: `team-1-unit-${unitId}`, position: rotate(position), facing: 'West' },
    ]),
    tileTags: [
      { id: 'home-pads', kind: 'SpawnProtected', tiles: [...westPad, ...eastPad] },
      {
        id: 'arc-required-tiles',
        kind: 'SignaturePlacementForbidden',
        tiles: [point(11, 6), point(15, 13), point(19, 20), point(2, 5), point(28, 21)],
      },
    ],
  };
}

/**
 * Public review remains read-only: these responses do not mutate fixture state. They
 * only let the Arena composer complete its frontend handoff to an existing reviewed
 * set or match, so a phone walkthrough does not end at an artificial 409.
 */
type ReviewArenaLaunchKind = 'ranked' | 'challenge' | 'labs';

function reviewArenaLaunchKind(
  method: string | undefined,
  url: URL,
): ReviewArenaLaunchKind | null {
  if (method?.toUpperCase() !== 'POST' || url.search !== '') return null;
  if (url.pathname === '/api/matches/ranked') return 'ranked';
  if (url.pathname === '/api/matches/challenge') return 'challenge';
  if (url.pathname === '/api/labs/matches') return 'labs';
  return null;
}

async function handleReviewArenaLaunch(
  request: Parameters<Connect.NextHandleFunction>[0],
  response: Parameters<Connect.NextHandleFunction>[1],
  kind: ReviewArenaLaunchKind,
) {
  try {
    const body = await readJsonRequest(request);
    const launch = reviewArenaLaunch(kind, body);
    if (launch === null) {
      response.setHeader('x-nilbots-site-review-expected-status', '409');
      response.setHeader(
        'x-nilbots-site-review-error',
        'uncanned-arena-launch',
      );
      sendJson(response, 409, {
        type: 'about:blank',
        title: 'Review matchup is not simulated',
        status: 409,
        detail:
          'This read-only review can launch ranked Pincer or the Pincer vs Warden matchup. Choose those bots to inspect the destination flow.',
      });
      return;
    }
    response.setHeader('x-nilbots-site-review-simulated', '1');
    sendJson(response, 200, launch);
  } catch {
    response.setHeader('x-nilbots-site-review-error', 'invalid-launch-body');
    sendJson(response, 400, {
      type: 'about:blank',
      title: 'Invalid review launch',
      status: 400,
      detail: 'The Arena launch request was not valid JSON.',
    });
  }
}

function reviewArenaLaunch(
  kind: ReviewArenaLaunchKind,
  body: unknown,
): { id: string } | null {
  if (!isRecord(body)) return null;
  if (kind === 'ranked') {
    return body.botId === REVIEW_PINCER_ID ? { id: REVIEW_SET_ID } : null;
  }
  if (kind === 'challenge') {
    return body.botId === REVIEW_PINCER_ID &&
      body.opponentBotId === REVIEW_WARDEN_ID
      ? { id: REVIEW_LAUNCHED_MATCH_ID }
      : null;
  }
  return body.playlistVersionId === REVIEW_LABS_PLAYLIST_ID &&
    Array.isArray(body.entrantBotIds) &&
    body.entrantBotIds.length === 2 &&
    body.entrantBotIds[0] === REVIEW_PINCER_ID &&
    body.entrantBotIds[1] === REVIEW_WARDEN_ID
    ? { id: REVIEW_LAUNCHED_MATCH_ID }
    : null;
}

function readJsonRequest(
  request: Parameters<Connect.NextHandleFunction>[0],
): Promise<unknown> {
  return new Promise((resolve, reject) => {
    let body = '';
    request.setEncoding('utf8');
    request.on('data', (chunk: string) => {
      body += chunk;
      if (body.length > 16_384) {
        reject(new Error('Review request body exceeded 16 KiB.'));
        request.destroy();
      }
    });
    request.on('end', () => {
      try {
        resolve(JSON.parse(body));
      } catch (error) {
        reject(error);
      }
    });
    request.on('error', reject);
  });
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function sendJson(
  response: Parameters<Connect.NextHandleFunction>[1],
  status: number,
  body: unknown,
) {
  response.statusCode = status;
  response.setHeader('content-type', 'application/json; charset=utf-8');
  response.setHeader('cache-control', 'no-store');
  response.setHeader(SITE_REVIEW_HEADER, '1');
  response.end(JSON.stringify(body));
}

function sendRawJson(
  response: Parameters<Connect.NextHandleFunction>[1],
  status: number,
  body: string,
) {
  response.statusCode = status;
  response.setHeader('content-type', 'application/json; charset=utf-8');
  response.setHeader('cache-control', 'no-store');
  response.setHeader(SITE_REVIEW_HEADER, '1');
  response.end(body);
}

function reviewReplay(method: string | undefined, url: URL): string | null {
  if (method?.toUpperCase() !== 'GET' || url.search !== '') return null;
  const completedReplay = completedReviewReplays.get(url.pathname);
  if (completedReplay !== undefined) return completedReplay;
  if (url.pathname === `/api/matches/${REVIEW_LIVE_MATCH_ID}/replay`) {
    return liveReviewReplay;
  }
  if (url.pathname === `/api/matches/${REVIEW_LAUNCHED_MATCH_ID}/replay`) {
    return launchedMatchReplay;
  }
  return null;
}

/**
 * Keep the checked-in engine fixture byte-for-byte canonical except for presentation
 * identity. Re-hashing the canonical payload means the completed review replay still
 * demonstrates the product's real verification contract.
 */
function makeCompletedReviewReplay(
  source: string,
  spec: ReviewSetGameSpec,
): string {
  const withoutHash = source.replace(
    /,"replayHash":"[0-9a-f]{64}"}$/,
    '}',
  );
  if (withoutHash === source) {
    throw new Error('The review replay fixture has no terminal replay hash.');
  }

  const pincer =
    '"name":"Pincer gen-10","runtimeKind":"wasm","artifactHash":"9f31c0a4b7de51aa","accent":"#22d3ee"';
  const bastille =
    '"name":"Bastille gen-5","runtimeKind":"wasm","artifactHash":"11b2b6bf82cf61e9","accent":"#ef4444"';
  let payload = replaceOnce(
    withoutHash,
    '"name":"hunter","runtimeKind":"wasm","artifactHash":"66d522119ded08eb14784f5939a3b9b278eeba927f54fa47090c970f3f14669e","accent":"#f97316"',
    spec.pincerSlot === 0 ? pincer : bastille,
  );
  payload = replaceOnce(
    payload,
    '"name":"Rampart gen-2","runtimeKind":"wasm","artifactHash":"8786634a943703ba456a2db7521afad86b8899c72f8c65ab3fdc3fa299b542a3","accent":"#a78bfa"',
    spec.pincerSlot === 0 ? bastille : pincer,
  );
  if (spec.pincerSlot === 0) {
    payload = replaceOnce(
      payload,
      '"lookId":"orbiter"',
      '"lookId":"bulwark"',
    );
  } else {
    payload = replaceOnce(
      payload,
      '"lookId":"vanguard"',
      '"lookId":"bulwark"',
    );
    payload = replaceOnce(
      payload,
      '"lookId":"orbiter"',
      '"lookId":"vanguard"',
    );
  }
  if (spec.mapId !== 'arena-01') {
    payload = replaceOnce(
      payload,
      '"mapId":"arena-01"',
      `"mapId":"${spec.mapId}"`,
    );
  }
  if (spec.themeId !== 'ember-forge') {
    payload = replaceOnce(
      payload,
      '"themeId":"ember-forge"',
      `"themeId":"${spec.themeId}"`,
    );
  }

  const hash = createHash('sha256').update(payload).digest('hex');
  if (hash !== spec.replayHash) {
    throw new Error(
      `Review replay hash drifted for game ${spec.game}: expected ${spec.replayHash}, received ${hash}.`,
    );
  }
  return `${payload.slice(0, -1)},"replayHash":"${hash}"}`;
}

interface LiveReviewParticipant {
  readonly name: string;
  readonly artifactHash: string;
  readonly accent: string;
  readonly lookId: string;
}

function makeLiveReviewReplay(
  source: string,
  participants: readonly [LiveReviewParticipant, LiveReviewParticipant],
): string {
  const document = JSON.parse(source) as {
    header: {
      participants: Array<{
        name: string;
        artifactHash: string;
        accent: string;
        lookId?: string;
      }>;
    };
    ticks: unknown[];
    result?: unknown;
    replayHash?: unknown;
    partial?: boolean;
  };
  const [first, second] = document.header.participants;
  if (!first || !second) {
    throw new Error('The review replay needs two participants.');
  }
  Object.assign(first, participants[0]);
  Object.assign(second, participants[1]);
  document.ticks = document.ticks.slice(0, 25);
  delete document.result;
  delete document.replayHash;
  document.partial = true;
  return JSON.stringify(document);
}

function replaceOnce(source: string, from: string, to: string): string {
  const first = source.indexOf(from);
  if (first < 0 || source.indexOf(from, first + from.length) >= 0) {
    throw new Error(`Expected exactly one review replay fragment: ${from.slice(0, 48)}…`);
  }
  return `${source.slice(0, first)}${to}${source.slice(first + from.length)}`;
}

export default defineConfig({
  plugins: [siteReviewApi(), react(), tailwindcss()],
  define: {
    'import.meta.env.VITE_SITE_REVIEW': JSON.stringify('1'),
  },
  build: {
    outDir: 'dist-site-review',
  },
  server: {
    host: true,
    port: 4181,
    strictPort: true,
  },
  preview: {
    host: true,
    port: 4181,
    strictPort: true,
    allowedHosts: ['.trycloudflare.com', '.ngrok-free.app', '.ngrok.io', '.ts.net'],
  },
});
